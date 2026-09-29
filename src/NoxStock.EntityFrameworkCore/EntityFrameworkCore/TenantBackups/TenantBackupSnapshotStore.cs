using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using NoxStock.Accounts;
using NoxStock.Brands;
using NoxStock.BusinessPartners;
using NoxStock.BusinessPartnerTransfers;
using NoxStock.Categories;
using NoxStock.Documents;
using NoxStock.EntityFrameworkCore;
using NoxStock.InventoryCounts;
using NoxStock.Products;
using NoxStock.StockMovements;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace NoxStock.TenantBackups;

/// <summary>
/// Explicit commercial-data whitelist used by tenant backup V1. Platform, identity,
/// licensing, exchange-rate and backup metadata tables are deliberately absent.
/// </summary>
public sealed class TenantBackupSnapshotStore : ITenantBackupSnapshotStore, ITransientDependency
{
    private static readonly IReadOnlyDictionary<string, Type[]> Modules = new Dictionary<string, Type[]>(StringComparer.Ordinal)
    {
        ["catalog"] = [typeof(Brand), typeof(Category), typeof(Product), typeof(ProductOEMCode)],
        ["parties-accounts"] = [typeof(BusinessPartner), typeof(Account), typeof(BusinessPartnerOpeningBalance), typeof(BusinessPartnerTransfer)],
        ["documents"] = [typeof(Document), typeof(DocumentItem), typeof(DocumentExchangeRateSnapshot), typeof(DocumentNumberSequence)],
        ["inventory-cost"] = [typeof(StockMovement), typeof(ProductCostHistory), typeof(InventoryCount), typeof(InventoryCountItem)],
        ["finance"] = [typeof(AccountTransaction), typeof(PaymentAllocation)],
        ["completed-commands"] = [typeof(CommercialCommand)]
    };

    // Clone remapping is intentionally explicit. Only the listed commercial
    // identities and relationships may be rewritten; audit-user identities and
    // other arbitrary Guid columns are never inferred through reflection.
    private static readonly IReadOnlyDictionary<Type, IReadOnlyDictionary<string, Type>> CloneReferences =
        new Dictionary<Type, IReadOnlyDictionary<string, Type>>
        {
            [typeof(Category)] = Refs(("ParentId", typeof(Category))),
            [typeof(Product)] = Refs(
                ("CategoryId", typeof(Category)), ("BrandId", typeof(Brand)),
                ("LastPurchaseDocumentId", typeof(Document)), ("LastPurchaseDocumentItemId", typeof(DocumentItem)),
                ("LastPurchaseCostHistoryId", typeof(ProductCostHistory)), ("LastPurchaseSupplierId", typeof(BusinessPartner))),
            [typeof(ProductOEMCode)] = Refs(("ProductId", typeof(Product))),
            [typeof(BusinessPartnerOpeningBalance)] = Refs(
                ("BusinessPartnerId", typeof(BusinessPartner)), ("ReversalOfOpeningBalanceId", typeof(BusinessPartnerOpeningBalance))),
            [typeof(BusinessPartnerTransfer)] = Refs(
                ("CustomerId", typeof(BusinessPartner)), ("SupplierId", typeof(BusinessPartner))),
            [typeof(Document)] = Refs(
                ("BusinessPartnerId", typeof(BusinessPartner)), ("SourceDocumentId", typeof(Document))),
            [typeof(DocumentItem)] = Refs(
                ("DocumentId", typeof(Document)), ("SourceDocumentItemId", typeof(DocumentItem)),
                ("ProductId", typeof(Product)), ("ProductCostHistoryId", typeof(ProductCostHistory))),
            [typeof(DocumentExchangeRateSnapshot)] = Refs(("DocumentId", typeof(Document))),
            [typeof(StockMovement)] = Refs(("ProductId", typeof(Product)), ("DocumentId", typeof(Document))),
            [typeof(ProductCostHistory)] = Refs(
                ("ProductId", typeof(Product)), ("SourceDocumentId", typeof(Document)),
                ("SourceDocumentItemId", typeof(DocumentItem)), ("SupplierId", typeof(BusinessPartner))),
            [typeof(InventoryCountItem)] = Refs(
                ("InventoryCountId", typeof(InventoryCount)), ("ProductId", typeof(Product))),
            [typeof(AccountTransaction)] = Refs(
                ("AccountId", typeof(Account)), ("DocumentId", typeof(Document)),
                ("BusinessPartnerId", typeof(BusinessPartner)), ("RelatedAccountId", typeof(Account)),
                ("ReversalOfTransactionId", typeof(AccountTransaction))),
            [typeof(PaymentAllocation)] = Refs(
                ("AccountTransactionId", typeof(AccountTransaction)), ("DocumentId", typeof(Document)),
                ("BusinessPartnerId", typeof(BusinessPartner))),
            [typeof(CommercialCommand)] = Refs(("DocumentId", typeof(Document)))
        };

    private static readonly HashSet<string> CloneUserColumns = new(StringComparer.Ordinal)
    {
        "CreatorId", "LastModifierId", "DeleterId", "CancelledBy", "ReversedBy", "CompletedByUserId"
    };

    private readonly IDbContextProvider<NoxStockDbContext> _contexts;

    public TenantBackupSnapshotStore(IDbContextProvider<NoxStockDbContext> contexts) => _contexts = contexts;

    public async Task<IReadOnlyList<TenantBackupDataFile>> ExportModuleAsync(string moduleName, Guid tenantId)
    {
        var db = await _contexts.GetDbContextAsync();
        await db.Database.OpenConnectionAsync();
        var files = new List<TenantBackupDataFile>();
        foreach (var type in GetModule(moduleName))
        {
            var entity = GetEntity(db, type);
            var rows = await ReadRowsAsync(db, entity, tenantId, type == typeof(CommercialCommand));
            var content = Encoding.UTF8.GetBytes(string.Join('\n', rows.Select(x => JsonSerializer.Serialize(x))) + (rows.Count == 0 ? string.Empty : "\n"));
            files.Add(new TenantBackupDataFile(FileName(moduleName, entity), rows.Count, content));
        }
        return files;
    }

    public async Task ImportModuleAsync(string moduleName, TenantBackupImportContext context, IReadOnlyDictionary<string, byte[]> files)
    {
        var db = await _contexts.GetDbContextAsync();
        await db.Database.OpenConnectionAsync();
        foreach (var type in GetModule(moduleName))
        {
            var entity = GetEntity(db, type);
            if (!files.TryGetValue(FileName(moduleName, entity), out var content))
                throw new BusinessException("NoxStock:BackupModuleMissing");

            var rows = ParseRows(content);
            SortRows(type, rows);
            foreach (var row in rows)
                await InsertRowAsync(db, entity, type, row, context, deferCostHistoryLink: type == typeof(DocumentItem));
        }

        // DocumentItem <-> ProductCostHistory is a nullable cycle. Items are first
        // inserted without the history link; after cost rows exist the original link
        // is restored from the already validated document JSONL file.
        if (moduleName == "inventory-cost")
            await RestoreDocumentItemCostLinksAsync(db, context, files);
    }

    public async Task<bool> IsTenantDataEmptyAsync(Guid tenantId)
    {
        var db = await _contexts.GetDbContextAsync();
        await db.Database.OpenConnectionAsync();
        foreach (var type in Modules.Values.SelectMany(x => x).Distinct())
        {
            var entity = GetEntity(db, type);
            await using var command = CreateCommand(db, $"SELECT EXISTS (SELECT 1 FROM {Table(entity)} WHERE \"TenantId\" = @tenant LIMIT 1)");
            Add(command, "tenant", tenantId);
            if (Convert.ToBoolean(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture)) return false;
        }
        return true;
    }

    public async Task<string> GetSchemaVersionAsync()
    {
        var db = await _contexts.GetDbContextAsync();
        return (await db.Database.GetAppliedMigrationsAsync()).LastOrDefault() ?? "none";
    }

    public async Task<TenantBackupInvariantSummary> GetInvariantSummaryAsync(Guid tenantId)
    {
        var db = await _contexts.GetDbContextAsync();
        var products = await db.Products.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ToListAsync();
        var partners = await db.BusinessPartners.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ToListAsync();
        var documents = await db.Documents.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ToListAsync();
        var accounts = await db.Accounts.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ToListAsync();
        return new TenantBackupInvariantSummary(
            products.Count, partners.Count, documents.Count, accounts.Count,
            products.Sum(x => x.Quantity), accounts.Sum(x => x.Balance),
            partners.Sum(x => x.Balance), documents.Sum(x => x.GrandTotal));
    }

    private async Task<List<Dictionary<string, object?>>> ReadRowsAsync(NoxStockDbContext db, IEntityType entity, Guid tenantId, bool terminalCommandsOnly)
    {
        var columns = Columns(entity);
        var sql = $"SELECT {string.Join(", ", columns.Select(x => Quote(x.ColumnName)))} FROM {Table(entity)} WHERE \"TenantId\" = @tenant";
        if (terminalCommandsOnly) sql += " AND \"Status\" <> 1";
        sql += " ORDER BY \"Id\"";
        await using var command = CreateCommand(db, sql);
        Add(command, "tenant", tenantId);
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < columns.Count; i++)
                row[columns[i].ColumnName] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
            result.Add(row);
        }
        return result;
    }

    private async Task InsertRowAsync(NoxStockDbContext db, IEntityType entity, Type entityType,
        Dictionary<string, JsonElement> row, TenantBackupImportContext context, bool deferCostHistoryLink)
    {
        var columns = Columns(entity).Where(x => row.ContainsKey(x.ColumnName)).ToList();
        var names = new List<string>();
        var values = new List<string>();
        await using var command = CreateCommand(db, string.Empty);
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            names.Add(Quote(column.ColumnName));
            var parameterName = $"p{i}";
            var value = column.ColumnName == "TenantId"
                ? context.TargetTenantId
                : ConvertImportValue(entityType, column, row[column.ColumnName], context);
            if (deferCostHistoryLink && column.ColumnName == "ProductCostHistoryId") value = null;
            Add(command, parameterName, value);
            values.Add(column.StoreType.Contains("json", StringComparison.OrdinalIgnoreCase)
                ? $"CAST(@{parameterName} AS {column.StoreType})"
                : $"@{parameterName}");
        }
        command.CommandText = $"INSERT INTO {Table(entity)} ({string.Join(", ", names)}) VALUES ({string.Join(", ", values)})";
        if (await command.ExecuteNonQueryAsync() != 1) throw new BusinessException("NoxStock:BackupImportWriteConflict");
    }

    private async Task RestoreDocumentItemCostLinksAsync(NoxStockDbContext db, TenantBackupImportContext context,
        IReadOnlyDictionary<string, byte[]> files)
    {
        var entity = GetEntity(db, typeof(DocumentItem));
        if (!files.TryGetValue(FileName("documents", entity), out var content)) return;
        foreach (var row in ParseRows(content))
        {
            if (!row.TryGetValue("ProductCostHistoryId", out var cost) || cost.ValueKind == JsonValueKind.Null) continue;
            await using var command = CreateCommand(db, $"UPDATE {Table(entity)} SET \"ProductCostHistoryId\" = @cost WHERE \"Id\" = @id AND \"TenantId\" = @tenant");
            Add(command, "cost", context.Map(typeof(ProductCostHistory), cost.GetGuid()));
            Add(command, "id", context.Map(typeof(DocumentItem), row["Id"].GetGuid()));
            Add(command, "tenant", context.TargetTenantId);
            if (await command.ExecuteNonQueryAsync() != 1) throw new BusinessException("NoxStock:BackupImportWriteConflict");
        }
    }

    private static void SortRows(Type type, List<Dictionary<string, JsonElement>> rows)
    {
        var parentColumn = type == typeof(Category) ? "ParentId" :
            type == typeof(Document) ? "SourceDocumentId" :
            type == typeof(DocumentItem) ? "SourceDocumentItemId" :
            type == typeof(BusinessPartnerOpeningBalance) ? "ReversalOfOpeningBalanceId" :
            type == typeof(AccountTransaction) ? "ReversalOfTransactionId" : null;
        if (parentColumn != null)
            rows.Sort((a, b) => IsNull(a, parentColumn).CompareTo(IsNull(b, parentColumn)) * -1);
    }

    private static bool IsNull(Dictionary<string, JsonElement> row, string key) => !row.TryGetValue(key, out var value) || value.ValueKind == JsonValueKind.Null;
    private static List<Dictionary<string, JsonElement>> ParseRows(byte[] content) => Encoding.UTF8.GetString(content)
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(x => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(x) ?? throw new BusinessException("NoxStock:InvalidBackupFormat"))
        .ToList();

    private static IEntityType GetEntity(NoxStockDbContext db, Type type) => db.Model.FindEntityType(type) ?? throw new InvalidOperationException($"Entity mapping missing: {type.Name}");
    private static Type[] GetModule(string name) => Modules.TryGetValue(name, out var types) ? types : throw new BusinessException("NoxStock:UnknownBackupModule");
    private static string FileName(string module, IEntityType entity) => $"{module}/{entity.GetTableName()}.jsonl";
    private static string Quote(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    private static string Table(IEntityType entity) => string.IsNullOrWhiteSpace(entity.GetSchema()) ? Quote(entity.GetTableName()!) : $"{Quote(entity.GetSchema()!)}.{Quote(entity.GetTableName()!)}";

    private static List<ColumnInfo> Columns(IEntityType entity)
    {
        var store = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
        return entity.GetProperties().Select(x => new ColumnInfo(x.GetColumnName(store)!, x.GetColumnType() ?? x.GetRelationalTypeMapping().StoreType)).OrderBy(x => x.ColumnName, StringComparer.Ordinal).ToList();
    }

    private static DbCommand CreateCommand(NoxStockDbContext db, string sql)
    {
        var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        return command;
    }

    private static void Add(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static object? ConvertValue(JsonElement value, string storeType)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        var type = storeType.ToLowerInvariant();
        if (type.Contains("uuid")) return value.GetGuid();
        if (type.Contains("bool")) return value.GetBoolean();
        if (type.Contains("smallint")) return value.GetInt16();
        if (type.Contains("bigint")) return value.GetInt64();
        if (type.Contains("int")) return value.GetInt32();
        if (type.Contains("numeric") || type.Contains("decimal")) return value.GetDecimal();
        if (type.Contains("timestamp")) return value.GetDateTime();
        if (type.Contains("bytea")) return value.GetBytesFromBase64();
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
    }

    private static object? ConvertImportValue(Type entityType, ColumnInfo column, JsonElement value,
        TenantBackupImportContext context)
    {
        if (!context.RemapIds) return ConvertValue(value, column.StoreType);
        if (CloneUserColumns.Contains(column.ColumnName)) return null;
        if (column.ColumnName == "ExecutionToken") return null;
        if (value.ValueKind == JsonValueKind.Null) return null;
        if (column.ColumnName == "Id") return context.Map(entityType, value.GetGuid());
        if (CloneReferences.TryGetValue(entityType, out var references) &&
            references.TryGetValue(column.ColumnName, out var targetType))
            return context.Map(targetType, value.GetGuid());
        return ConvertValue(value, column.StoreType);
    }

    private static IReadOnlyDictionary<string, Type> Refs(params (string Column, Type Type)[] references) =>
        references.ToDictionary(x => x.Column, x => x.Type, StringComparer.Ordinal);

    private sealed record ColumnInfo(string ColumnName, string StoreType);
}
