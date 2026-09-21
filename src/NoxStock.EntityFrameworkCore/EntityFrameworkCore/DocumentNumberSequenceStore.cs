using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NoxStock.Documents;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace NoxStock.EntityFrameworkCore;
public sealed class DocumentNumberSequenceStore : IDocumentNumberSequenceStore, ITransientDependency
{
    private readonly IDbContextProvider<NoxStockDbContext> _contexts;
    public DocumentNumberSequenceStore(IDbContextProvider<NoxStockDbContext> contexts) => _contexts = contexts;
    public async Task<int?> TryAllocateAsync(Guid tenantId, DocumentType type, int year, string prefix)
    {
        var db = await _contexts.GetDbContextAsync();
        if (!db.Database.IsNpgsql()) return null;
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed) await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "INSERT INTO \"AppDocumentNumberSequences\" (\"Id\", \"TenantId\", \"DocumentType\", \"Year\", \"Prefix\", \"LastNumber\", \"CreationTime\", \"ExtraProperties\", \"ConcurrencyStamp\", \"IsDeleted\") VALUES (@id, @tenantId, @type, @year, @prefix, 1, @now, @extra, @stamp, FALSE) ON CONFLICT (\"TenantId\", \"DocumentType\", \"Year\", \"Prefix\") WHERE \"IsDeleted\" = FALSE DO UPDATE SET \"LastNumber\" = \"AppDocumentNumberSequences\".\"LastNumber\" + 1 RETURNING \"LastNumber\";";
        Add(command, "@id", Guid.NewGuid()); Add(command, "@tenantId", tenantId); Add(command, "@type", (int)type);
        Add(command, "@year", year); Add(command, "@prefix", prefix); Add(command, "@now", DateTime.UtcNow);
        Add(command, "@extra", "{}"); Add(command, "@stamp", Guid.NewGuid().ToString("N"));
        var value = await command.ExecuteScalarAsync();
        if (wasClosed) await connection.CloseAsync();
        return Convert.ToInt32(value);
    }

    private static void Add(System.Data.Common.DbCommand command, string name, object value)
    { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value; command.Parameters.Add(parameter); }
}
