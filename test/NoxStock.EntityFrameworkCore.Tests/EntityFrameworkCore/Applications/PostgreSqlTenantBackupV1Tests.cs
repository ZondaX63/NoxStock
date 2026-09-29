using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NoxStock.Accounts;
using NoxStock.Brands;
using NoxStock.BusinessPartners;
using NoxStock.BusinessPartnerTransfers;
using NoxStock.Categories;
using NoxStock.Documents;
using NoxStock.InventoryCounts;
using NoxStock.Products;
using NoxStock.StockMovements;
using NoxStock.Settings;
using NoxStock.TenantBackups;
using Shouldly;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Content;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Volo.Abp.Settings;
using Volo.Abp.SettingManagement;

namespace NoxStock.EntityFrameworkCore.Applications;

public class PostgreSqlTenantBackupV1Tests : NoxStockEntityFrameworkCoreTestBase
{
    private const string Password = "correct horse battery staple";
    private static readonly string[] CommercialTablesInDeleteOrder =
    [
        "AppPaymentAllocations", "AppAccountTransactions", "AppStockMovements", "AppProductCostHistories",
        "AppDocumentExchangeRateSnapshots", "AppCommercialCommands", "AppDocumentItems", "AppDocuments",
        "AppDocumentNumberSequences", "AppInventoryCountItems", "AppInventoryCounts", "AppProductOEMCodes",
        "AppProducts", "AppBusinessPartnerOpeningBalances", "AppBusinessPartnerTransfers", "AppAccounts",
        "AppBusinessPartners", "AppCategories", "AppBrands"
    ];

    [PostgreSqlFact]
    public async Task Representative_Tenant_Round_Trip_Preserves_All_Commercial_Data_And_Links()
    {
        var ids = await SeedRepresentativeTenantAsync(NoxStockTestConsts.TenantId, "V1-A");
        var packageBytes = await ExportAsync(NoxStockTestConsts.TenantId, "source-a");
        TenantBackupManifest expected;
        IReadOnlyDictionary<string, byte[]> files;
        using (var scope = ServiceProvider.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(NoxStockTestConsts.TenantId))
        using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false))
        {
            var result = await scope.ServiceProvider.GetRequiredService<TenantBackupPackageService>()
                .ReadAndValidateAsync(new MemoryStream(packageBytes), Password);
            expected = result.Manifest;
            files = result.Files;
            await uow.CompleteAsync();
        }

        await DeleteCommercialDataAsync(NoxStockTestConsts.TenantId);
        await ImportAsync(NoxStockTestConsts.TenantId, files);

        using var verifyScope = ServiceProvider.CreateScope();
        using var tenant = verifyScope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(NoxStockTestConsts.TenantId);
        using var verifyUow = verifyScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
        var db = await verifyScope.ServiceProvider.GetRequiredService<IDbContextProvider<NoxStockDbContext>>().GetDbContextAsync();
        var actual = await verifyScope.ServiceProvider.GetRequiredService<ITenantBackupSnapshotStore>().GetInvariantSummaryAsync(NoxStockTestConsts.TenantId);
        AssertInvariants(expected.Invariants, actual);
        (await db.Brands.CountAsync(x => x.TenantId == NoxStockTestConsts.TenantId)).ShouldBe(1);
        (await db.Categories.CountAsync(x => x.TenantId == NoxStockTestConsts.TenantId)).ShouldBe(1);
        (await db.ProductOEMCodes.CountAsync(x => x.TenantId == NoxStockTestConsts.TenantId)).ShouldBe(1);
        (await db.StockMovements.CountAsync(x => x.TenantId == NoxStockTestConsts.TenantId)).ShouldBe(1);
        (await db.ProductCostHistories.CountAsync(x => x.TenantId == NoxStockTestConsts.TenantId)).ShouldBe(1);
        (await db.InventoryCounts.CountAsync(x => x.TenantId == NoxStockTestConsts.TenantId)).ShouldBe(1);
        (await db.InventoryCountItems.CountAsync(x => x.TenantId == NoxStockTestConsts.TenantId)).ShouldBe(1);
        (await db.PaymentAllocations.CountAsync(x => x.TenantId == NoxStockTestConsts.TenantId)).ShouldBe(1);
        (await db.DocumentNumberSequences.SingleAsync(x => x.TenantId == NoxStockTestConsts.TenantId)).LastNumber.ShouldBe(41);
        var returned = await db.Documents.SingleAsync(x => x.Id == ids.ReturnDocumentId);
        returned.SourceDocumentId.ShouldBe(ids.SourceDocumentId);
        var returnedItem = await db.DocumentItems.SingleAsync(x => x.Id == ids.ReturnItemId);
        returnedItem.SourceDocumentItemId.ShouldBe(ids.SourceItemId);
        var source = await db.Documents.SingleAsync(x => x.Id == ids.SourceDocumentId);
        source.CompanyNameSnapshot.ShouldBe("Snapshot Company V1-A");
        var sourceItem = await db.DocumentItems.SingleAsync(x => x.Id == ids.SourceItemId);
        sourceItem.ProductNameSnapshot.ShouldBe("Snapshot Product V1-A");
        sourceItem.ProductCostHistoryId.ShouldBe(ids.CostHistoryId);
        var allocation = await db.PaymentAllocations.SingleAsync(x => x.TenantId == NoxStockTestConsts.TenantId);
        allocation.DocumentId.ShouldBe(ids.SourceDocumentId);
        allocation.AccountTransactionId.ShouldBe(ids.AccountTransactionId);
        await verifyUow.CompleteAsync();
    }

    [PostgreSqlFact]
    public async Task Export_Is_Tenant_Isolated_And_Excludes_Processing_Commands()
    {
        var tenantB = Guid.NewGuid();
        var a = await SeedRepresentativeTenantAsync(NoxStockTestConsts.TenantId, "ISO-A");
        var b = await SeedRepresentativeTenantAsync(tenantB, "ISO-B");
        await AddProcessingCommandAsync(NoxStockTestConsts.TenantId, a.SourceDocumentId);
        var bytes = await ExportAsync(NoxStockTestConsts.TenantId, "tenant-a");

        using var scope = ServiceProvider.CreateScope();
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(NoxStockTestConsts.TenantId))
        using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false))
        {
            var (_, files) = await scope.ServiceProvider.GetRequiredService<TenantBackupPackageService>()
                .ReadAndValidateAsync(new MemoryStream(bytes), Password);
            var allText = string.Join('\n', files.Values.Select(Encoding.UTF8.GetString));
            allText.ShouldContain(a.ProductId.ToString());
            allText.ShouldNotContain(b.ProductId.ToString());
            allText.ShouldNotContain("processing-command-must-not-export");
            await uow.CompleteAsync();
        }

        using var isolationScope = ServiceProvider.CreateScope();
        var currentTenant = isolationScope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var repository = isolationScope.ServiceProvider.GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<TenantBackup, Guid>>();
        var tenantBBackupId = Guid.NewGuid();
        using (currentTenant.Change(NoxStockTestConsts.TenantId))
        using (var uow = isolationScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
        {
            await repository.InsertAsync(new TenantBackup(Guid.NewGuid(), NoxStockTestConsts.TenantId, null, DateTime.UtcNow, DateTime.UtcNow.AddDays(1)));
            await uow.CompleteAsync();
        }
        using (currentTenant.Change(tenantB))
        using (var uow = isolationScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
        {
            (await repository.GetListAsync()).ShouldBeEmpty();
            await repository.InsertAsync(new TenantBackup(tenantBBackupId, tenantB, null, DateTime.UtcNow, DateTime.UtcNow.AddDays(1)));
            await uow.CompleteAsync();
        }

        using (currentTenant.Change(NoxStockTestConsts.TenantId))
        {
            var backups = isolationScope.ServiceProvider.GetRequiredService<ITenantBackupAppService>();
            await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(() => backups.GetDownloadTokenAsync(tenantBBackupId));
            await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(() => backups.DeleteAsync(tenantBBackupId));
        }
        using (currentTenant.Change(tenantB))
        using (var uow = isolationScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false))
        {
            (await repository.FindAsync(tenantBBackupId)).ShouldNotBeNull();
            await uow.CompleteAsync();
        }
    }

    [PostgreSqlFact]
    public async Task Import_Failure_Rolls_Back_All_Commercial_Rows()
    {
        await SeedRepresentativeTenantAsync(NoxStockTestConsts.TenantId, "ROLLBACK");
        var bytes = await ExportAsync(NoxStockTestConsts.TenantId, "rollback");
        IReadOnlyDictionary<string, byte[]> files;
        using (var scope = ServiceProvider.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(NoxStockTestConsts.TenantId))
        using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false))
        {
            files = (await scope.ServiceProvider.GetRequiredService<TenantBackupPackageService>().ReadAndValidateAsync(new MemoryStream(bytes), Password)).Files;
            await uow.CompleteAsync();
        }
        await DeleteCommercialDataAsync(NoxStockTestConsts.TenantId);

        using (var scope = ServiceProvider.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(NoxStockTestConsts.TenantId))
        using (scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
        {
            var contributors = scope.ServiceProvider.GetServices<ITenantBackupContributor>().OrderBy(x => x.Order).ToArray();
            await contributors.Single(x => x.ModuleName == "catalog").ImportAsync(
                new TenantBackupImportContext(NoxStockTestConsts.TenantId, NoxStockTestConsts.TenantId,
                    TenantBackupRestoreMode.LocalClonePreserveIds), files);
            // No CompleteAsync: simulates a failure after a module has written rows.
        }

        using var verifyScope = ServiceProvider.CreateScope();
        using var tenant = verifyScope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(NoxStockTestConsts.TenantId);
        using var verifyUow = verifyScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
        var db = await verifyScope.ServiceProvider.GetRequiredService<IDbContextProvider<NoxStockDbContext>>().GetDbContextAsync();
        (await db.Products.CountAsync(x => x.TenantId == NoxStockTestConsts.TenantId)).ShouldBe(0);
        await verifyUow.CompleteAsync();
    }

    [PostgreSqlFact]
    public async Task Wrong_Password_Tampered_Tag_And_Schema_Mismatch_Are_Rejected()
    {
        await SeedRepresentativeTenantAsync(NoxStockTestConsts.TenantId, "SECURITY");
        var bytes = await ExportAsync(NoxStockTestConsts.TenantId, "security");
        using var scope = ServiceProvider.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(NoxStockTestConsts.TenantId);
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
        var packages = scope.ServiceProvider.GetRequiredService<TenantBackupPackageService>();
        await Should.ThrowAsync<BusinessException>(() => packages.ReadAndValidateAsync(new MemoryStream(bytes), "incorrect password value"));
        var tampered = bytes.ToArray(); tampered[^9] ^= 0x20;
        await Should.ThrowAsync<BusinessException>(() => packages.ReadAndValidateAsync(new MemoryStream(tampered), Password));
        var hashMismatch = await TamperPayloadWithoutUpdatingManifestAsync(scope.ServiceProvider, bytes);
        (await Should.ThrowAsync<BusinessException>(() => packages.ReadAndValidateAsync(new MemoryStream(hashMismatch), Password))).Code
            .ShouldBe("NoxStock:BackupFileHashInvalid");
        var incompatible = await RewriteManifestAsync(scope.ServiceProvider, bytes, manifest => manifest.SchemaVersion += "-incompatible");
        (await Should.ThrowAsync<BusinessException>(() => packages.ReadAndValidateAsync(new MemoryStream(incompatible), Password))).Code
            .ShouldBe("NoxStock:BackupSchemaIncompatible");
        await uow.CompleteAsync();
    }

    [PostgreSqlFact]
    public async Task Restore_Creates_Clean_Local_Tenant_Rejects_Replay_And_Rolls_Back_Failed_Target()
    {
        var sourceId = Guid.NewGuid();
        await SeedRepresentativeTenantAsync(sourceId, "LOCAL-RESTORE");
        var bytes = await ExportAsync(sourceId, "local-source");
        await DeleteCommercialDataAsync(sourceId);

        using (var scope = ServiceProvider.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(null))
        {
            var restore = scope.ServiceProvider.GetRequiredService<ITenantRestoreAppService>();
            var result = await restore.RestoreToLocalOrNewTenantAsync(RestoreInput(bytes, "local-restored"));
            result.TenantId.ShouldBe(sourceId);
            (await scope.ServiceProvider.GetRequiredService<ITenantRepository>().FindAsync(sourceId)).ShouldNotBeNull();
            (await Should.ThrowAsync<BusinessException>(() => restore.RestoreToLocalOrNewTenantAsync(RestoreInput(bytes, "local-restored")))).Code
                .ShouldBe("NoxStock:BackupRestoreTargetExists");
        }

        var failingId = Guid.NewGuid();
        await SeedRepresentativeTenantAsync(failingId, "FAILED-RESTORE");
        var failingBytes = await ExportAsync(failingId, "failed-source");
        await DeleteCommercialDataAsync(failingId);
        var invalidFkPackage = await CorruptProductBrandLinkAsync(failingBytes);
        using (var scope = ServiceProvider.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(null))
        {
            var restore = scope.ServiceProvider.GetRequiredService<ITenantRestoreAppService>();
            await Should.ThrowAsync<Exception>(() => restore.RestoreToLocalOrNewTenantAsync(RestoreInput(invalidFkPackage, "failed-target")));
            (await scope.ServiceProvider.GetRequiredService<ITenantRepository>().FindAsync(failingId)).ShouldBeNull();
        }
        using (var scope = ServiceProvider.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(failingId))
        using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false))
        {
            var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<NoxStockDbContext>>().GetDbContextAsync();
            (await db.Products.IgnoreQueryFilters().CountAsync(x => x.TenantId == failingId)).ShouldBe(0);
            await uow.CompleteAsync();
        }
    }

    [PostgreSqlFact]
    public async Task Migration_Up_And_Down_Work_On_Empty_PostgreSql_Database()
    {
        var databaseName = $"noxstock_backup_migration_{Guid.NewGuid():N}";
        var admin = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NOXSTOCK_POSTGRES_TEST_CONNECTION")) { Database = "postgres" };
        await using (var connection = new NpgsqlConnection(admin.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }
        var target = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = databaseName };
        try
        {
            var options = new DbContextOptionsBuilder<NoxStockDbContext>().UseNpgsql(target.ConnectionString).Options;
            await using var db = new NoxStockDbContext(options);
            await db.Database.MigrateAsync();
            (await TableExistsAsync(target.ConnectionString, "AppTenantBackups")).ShouldBeTrue();
            (await TableExistsAsync(target.ConnectionString, "AppTenantCloneOperations")).ShouldBeTrue();
            await db.GetService<IMigrator>().MigrateAsync("20260913215254_AddDocumentNumberSequences");
            (await TableExistsAsync(target.ConnectionString, "AppTenantBackups")).ShouldBeFalse();
            (await TableExistsAsync(target.ConnectionString, "AppTenantCloneOperations")).ShouldBeFalse();
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(admin.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    [PostgreSqlFact]
    public async Task Download_Tokens_And_Restore_Environment_Are_Enforced()
    {
        using var scope = ServiceProvider.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        using (currentTenant.Change(NoxStockTestConsts.TenantId))
        {
            var backups = scope.ServiceProvider.GetRequiredService<ITenantBackupAppService>();
            (await Should.ThrowAsync<BusinessException>(() => backups.DownloadAsync(Guid.NewGuid(), "not-a-protected-token"))).Code
                .ShouldBe("NoxStock:BackupDownloadTokenInvalid");
            var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("NoxStock.TenantBackup.v1");
            var expiredId = Guid.NewGuid();
            var expired = protector.Protect(JsonSerializer.Serialize(new
            {
                Id = expiredId, TenantId = (Guid?)NoxStockTestConsts.TenantId, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5)
            }));
            (await Should.ThrowAsync<BusinessException>(() => backups.DownloadAsync(expiredId, expired))).Code
                .ShouldBe("NoxStock:BackupDownloadTokenInvalid");

            var restore = scope.ServiceProvider.GetRequiredService<ITenantRestoreAppService>();
            (await Should.ThrowAsync<BusinessException>(() => restore.ValidateAsync(new ValidateTenantBackupInputDto
            {
                Package = new RemoteStreamContent(new MemoryStream([1, 2, 3]), "invalid.noxbackup"), Password = Password
            }))).Code.ShouldBe("NoxStock:BackupRestoreLocalHostOnly");
        }

        var environment = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>();
        var original = environment.EnvironmentName;
        environment.EnvironmentName = Microsoft.Extensions.Hosting.Environments.Production;
        try
        {
            using (currentTenant.Change(null))
            {
                var restore = scope.ServiceProvider.GetRequiredService<ITenantRestoreAppService>();
                (await Should.ThrowAsync<BusinessException>(() => restore.ValidateAsync(new ValidateTenantBackupInputDto
                {
                    Package = new RemoteStreamContent(new MemoryStream([1, 2, 3]), "invalid.noxbackup"), Password = Password
                }))).Code.ShouldBe("NoxStock:BackupRestoreLocalHostOnly");
            }
        }
        finally { environment.EnvironmentName = original; }
    }

    [PostgreSqlFact]
    public async Task V2_NewTenantClone_Remaps_All_Ids_Links_And_Preserves_Source_And_Sequence()
    {
        var sourceTenantId = Guid.NewGuid();
        var sourceIds = await SeedRepresentativeTenantAsync(sourceTenantId, "V2-CLONE");
        var bytes = await ExportAsync(sourceTenantId, "v2-source");
        var targetName = $"V2 Clone {Guid.NewGuid():N}";
        var targetCode = $"v2-{Guid.NewGuid():N}";
        TenantBackupRestoreResultDto result;

        using (var scope = ServiceProvider.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(null))
        {
            var restore = scope.ServiceProvider.GetRequiredService<ITenantRestoreAppService>();
            var preview = await restore.PreviewAsync(new PreviewTenantBackupRestoreInputDto
            {
                Package = new RemoteStreamContent(new MemoryStream(bytes), "clone.noxbackup"), Password = Password,
                RestoreMode = TenantBackupRestoreMode.NewTenantCloneRemapIds,
                TargetTenantName = targetName, TargetTenantCode = targetCode
            });
            preview.IsCompatible.ShouldBeTrue();
            preview.TargetTenantId.ShouldBeNull();
            preview.ModuleRecordCounts["documents"].ShouldBeGreaterThan(0);

            result = await restore.RestoreToLocalOrNewTenantAsync(new RestoreTenantBackupInputDto
            {
                Package = new RemoteStreamContent(new MemoryStream(bytes), "clone.noxbackup"), Password = Password,
                RestoreMode = TenantBackupRestoreMode.NewTenantCloneRemapIds,
                TargetTenantName = targetName, TargetTenantCode = targetCode,
                AdminEmailAddress = $"admin-{Guid.NewGuid():N}@example.test", AdminPassword = "Test-Admin-Password-123!"
            });
        }

        result.TenantId.ShouldNotBe(sourceTenantId);
        result.TenantCode.ShouldBe(targetCode);
        result.RestoreMode.ShouldBe(TenantBackupRestoreMode.NewTenantCloneRemapIds);
        Guid targetProductId;

        using (var scope = ServiceProvider.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(result.TenantId, result.TenantCode))
        using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
        {
            var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<NoxStockDbContext>>().GetDbContextAsync();
            var targetProduct = await db.Products.SingleAsync(x => x.Sku == "SKU-V2-CLONE");
            targetProductId = targetProduct.Id;
            targetProduct.Id.ShouldNotBe(sourceIds.ProductId);
            (await db.Products.AnyAsync(x => x.Id == sourceIds.ProductId)).ShouldBeFalse();
            var sourceDocument = await db.Documents.SingleAsync(x => x.DocNumber == "SF-V2-CLONE");
            var returnDocument = await db.Documents.SingleAsync(x => x.DocNumber == "IADE-V2-CLONE");
            sourceDocument.Id.ShouldNotBe(sourceIds.SourceDocumentId);
            returnDocument.SourceDocumentId.ShouldBe(sourceDocument.Id);
            var sourceItem = await db.DocumentItems.SingleAsync(x => x.DocumentId == sourceDocument.Id);
            var returnItem = await db.DocumentItems.SingleAsync(x => x.DocumentId == returnDocument.Id);
            returnItem.SourceDocumentItemId.ShouldBe(sourceItem.Id);
            sourceItem.ProductId.ShouldBe(targetProduct.Id);
            sourceItem.ProductCostHistoryId.ShouldNotBeNull();
            (await db.ProductCostHistories.SingleAsync(x => x.Id == sourceItem.ProductCostHistoryId)).ProductId.ShouldBe(targetProduct.Id);
            var allocation = await db.PaymentAllocations.SingleAsync();
            allocation.DocumentId.ShouldBe(sourceDocument.Id);
            (await db.AccountTransactions.SingleAsync(x => x.Id == allocation.AccountTransactionId)).DocumentId.ShouldBe(sourceDocument.Id);
            sourceDocument.CompanyNameSnapshot.ShouldBe("Snapshot Company V2-CLONE");
            sourceItem.ProductNameSnapshot.ShouldBe("Snapshot Product V2-CLONE");
            var sequence = await db.DocumentNumberSequences.SingleAsync(x => x.Prefix == "SF");
            sequence.LastNumber.ShouldBe(41);
            (await scope.ServiceProvider.GetRequiredService<IDocumentNumberSequenceStore>()
                .TryAllocateAsync(result.TenantId, DocumentType.SaleInvoice, DateTime.UtcNow.Year, "SF")).ShouldBe(42);
            (await db.Users.IgnoreQueryFilters().CountAsync(x => x.TenantId == result.TenantId)).ShouldBeGreaterThan(0);
            var defaultAccount = await scope.ServiceProvider.GetRequiredService<ISettingProvider>().GetOrNullAsync(NoxStockSettings.DefaultCashAccountId);
            Guid.TryParse(defaultAccount, out var defaultAccountId).ShouldBeTrue();
            (await db.Accounts.AnyAsync(x => x.Id == defaultAccountId)).ShouldBeTrue();
            await uow.CompleteAsync();
        }

        using (var scope = ServiceProvider.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(sourceTenantId))
        using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false))
        {
            var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<NoxStockDbContext>>().GetDbContextAsync();
            (await db.Products.AnyAsync(x => x.Id == sourceIds.ProductId)).ShouldBeTrue();
            (await db.Products.AnyAsync(x => x.Id == targetProductId)).ShouldBeFalse();
            (await db.Documents.AnyAsync(x => x.Id == sourceIds.SourceDocumentId)).ShouldBeTrue();
            await uow.CompleteAsync();
        }
    }

    [PostgreSqlFact]
    public async Task V2_NewTenantClone_Rejects_Duplicate_Code_And_Display_Name()
    {
        var sourceTenantId = Guid.NewGuid();
        await SeedRepresentativeTenantAsync(sourceTenantId, "V2-DUP");
        var bytes = await ExportAsync(sourceTenantId, "v2-duplicate-source");
        var targetName = $"Duplicate Display {Guid.NewGuid():N}";
        var targetCode = $"dup-{Guid.NewGuid():N}";

        using var scope = ServiceProvider.CreateScope();
        using var host = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(null);
        var restore = scope.ServiceProvider.GetRequiredService<ITenantRestoreAppService>();
        await restore.RestoreToLocalOrNewTenantAsync(CloneInput(bytes, targetName, targetCode));
        (await Should.ThrowAsync<BusinessException>(() => restore.RestoreToLocalOrNewTenantAsync(
            CloneInput(bytes, "Another display", targetCode)))).Code.ShouldBe("NoxStock:BackupRestoreTargetExists");
        (await Should.ThrowAsync<BusinessException>(() => restore.RestoreToLocalOrNewTenantAsync(
            CloneInput(bytes, targetName, $"other-{Guid.NewGuid():N}")))).Code.ShouldBe("NoxStock:BackupRestoreTargetExists");
    }

    [PostgreSqlFact]
    public async Task V2_NewTenantClone_Failure_Rolls_Back_Target_And_All_Remapped_Data()
    {
        var sourceTenantId = Guid.NewGuid();
        await SeedRepresentativeTenantAsync(sourceTenantId, "V2-ROLLBACK");
        var bytes = await CorruptProductBrandLinkAsync(await ExportAsync(sourceTenantId, "v2-rollback-source"));
        var targetCode = $"failed-{Guid.NewGuid():N}";

        using (var scope = ServiceProvider.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(null))
        {
            var restore = scope.ServiceProvider.GetRequiredService<ITenantRestoreAppService>();
            await Should.ThrowAsync<Exception>(() => restore.RestoreToLocalOrNewTenantAsync(
                CloneInput(bytes, "Failed clone", targetCode)));
            (await scope.ServiceProvider.GetRequiredService<ITenantRepository>().FindByNameAsync(targetCode.ToUpperInvariant())).ShouldBeNull();
        }

        using var verifyScope = ServiceProvider.CreateScope();
        using var verifyUow = verifyScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
        var db = await verifyScope.ServiceProvider.GetRequiredService<IDbContextProvider<NoxStockDbContext>>().GetDbContextAsync();
        (await db.Products.IgnoreQueryFilters().CountAsync(x => x.Sku == "SKU-V2-ROLLBACK" && x.TenantId != sourceTenantId)).ShouldBe(0);
        (await db.Products.IgnoreQueryFilters().CountAsync(x => x.Sku == "SKU-V2-ROLLBACK" && x.TenantId == sourceTenantId)).ShouldBe(1);
        await verifyUow.CompleteAsync();
    }

    [PostgreSqlFact]
    public async Task Durable_Clone_Operation_Is_Idempotent_And_Commits_A_New_Tenant_Only_After_Import()
    {
        var sourceTenantId = Guid.NewGuid();
        await SeedRepresentativeTenantAsync(sourceTenantId, "DURABLE");
        var backupId = await CreateStoredBackupAsync(sourceTenantId, "durable-source");
        var targetCode = $"durable-{Guid.NewGuid():N}";
        var operationKey = $"clone-{Guid.NewGuid():N}";
        TenantCloneOperationDto operation;

        using (var scope = ServiceProvider.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(null))
        {
            var restore = scope.ServiceProvider.GetRequiredService<ITenantRestoreAppService>();
            operation = await restore.StartCloneAsync(new StartTenantCloneInputDto
            {
                SourceBackupId = backupId, TargetTenantName = "Durable clone", TargetTenantCode = targetCode,
                OperationKey = operationKey, Password = Password,
                AdminEmailAddress = $"admin-{Guid.NewGuid():N}@example.test", AdminPassword = "Test-Admin-Password-123!"
            });
            operation.Status.ShouldBe(TenantCloneOperationStatus.Queued);
            var replay = await restore.StartCloneAsync(new StartTenantCloneInputDto
            {
                SourceBackupId = backupId, TargetTenantName = "Durable clone", TargetTenantCode = targetCode,
                OperationKey = operationKey, Password = Password,
                AdminEmailAddress = $"other-{Guid.NewGuid():N}@example.test", AdminPassword = "Test-Admin-Password-123!"
            });
            replay.Id.ShouldBe(operation.Id);
        }

        using (var scope = ServiceProvider.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(null))
        {
            await scope.ServiceProvider.GetRequiredService<TenantCloneOperationExecutor>().ExecuteAsync(operation.Id);
        }

        using var verify = ServiceProvider.CreateScope();
        using var host = verify.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(null);
        var restoreVerify = verify.ServiceProvider.GetRequiredService<ITenantRestoreAppService>();
        var completed = await restoreVerify.GetCloneOperationAsync(operation.Id);
        completed.Status.ShouldBe(TenantCloneOperationStatus.Succeeded);
        completed.TargetTenantId.ShouldNotBeNull();
        var target = await verify.ServiceProvider.GetRequiredService<ITenantRepository>().FindAsync(completed.TargetTenantId!.Value);
        target.ShouldNotBeNull();
        target!.Name.ShouldBe(targetCode);
    }

    private async Task<byte[]> ExportAsync(Guid tenantId, string tenantName)
    {
        using var scope = ServiceProvider.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId, tenantName);
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true,
            isolationLevel: System.Data.IsolationLevel.RepeatableRead);
        await using var package = await scope.ServiceProvider.GetRequiredService<TenantBackupPackageService>().CreateAsync(tenantId, tenantName, Password);
        var bytes = package.ToArray();
        await uow.CompleteAsync();
        return bytes;
    }

    private async Task<Guid> CreateStoredBackupAsync(Guid tenantId, string tenantName)
    {
        var bytes = await ExportAsync(tenantId, tenantName);
        var id = Guid.NewGuid();
        var storageName = $"{tenantId:N}/{id:N}.noxbackup";
        using var scope = ServiceProvider.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId, tenantName);
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
        var backup = new TenantBackup(id, tenantId, null, DateTime.UtcNow, DateTime.UtcNow.AddDays(1));
        backup.MarkProcessing();
        backup.MarkSucceeded(DateTime.UtcNow, bytes.Length, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(), storageName);
        await scope.ServiceProvider.GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<TenantBackup, Guid>>().InsertAsync(backup, autoSave: true);
        await scope.ServiceProvider.GetRequiredService<ITenantBackupStorage>().SaveAsync(storageName, new MemoryStream(bytes));
        await uow.CompleteAsync();
        return id;
    }

    private static RestoreTenantBackupInputDto RestoreInput(byte[] package, string tenantName) => new()
    {
        Package = new RemoteStreamContent(new MemoryStream(package), "test.noxbackup", "application/octet-stream"),
        Password = Password, TargetTenantName = tenantName,
        AdminEmailAddress = $"admin-{Guid.NewGuid():N}@example.test", AdminPassword = "Test-Admin-Password-123!"
    };

    private static RestoreTenantBackupInputDto CloneInput(byte[] package, string tenantName, string tenantCode) => new()
    {
        Package = new RemoteStreamContent(new MemoryStream(package), "clone.noxbackup", "application/octet-stream"),
        Password = Password, RestoreMode = TenantBackupRestoreMode.NewTenantCloneRemapIds,
        TargetTenantName = tenantName, TargetTenantCode = tenantCode,
        AdminEmailAddress = $"admin-{Guid.NewGuid():N}@example.test", AdminPassword = "Test-Admin-Password-123!"
    };

    private async Task<byte[]> CorruptProductBrandLinkAsync(byte[] package)
    {
        using var scope = ServiceProvider.CreateScope();
        var encryption = scope.ServiceProvider.GetRequiredService<IBackupEncryptionService>();
        await using var zip = new MemoryStream();
        await encryption.DecryptAsync(new MemoryStream(package), zip, Password);
        zip.Position = 0;
        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true))
        {
            foreach (var entry in archive.Entries)
            {
                await using var source = entry.Open(); await using var copy = new MemoryStream();
                await source.CopyToAsync(copy); entries[entry.FullName] = copy.ToArray();
            }
        }
        const string productFile = "catalog/AppProducts.jsonl";
        var lines = Encoding.UTF8.GetString(entries[productFile]).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var row = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(lines[0])!;
        row["BrandId"] = JsonSerializer.SerializeToElement(Guid.NewGuid());
        entries[productFile] = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(row) + "\n");
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var manifest = JsonSerializer.Deserialize<TenantBackupManifest>(entries["manifest.json"], jsonOptions)!;
        manifest.Files.Single(x => x.Name == productFile).Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(entries[productFile])).ToLowerInvariant();
        entries["manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(manifest, jsonOptions);
        await using var rewritten = new MemoryStream();
        using (var output = new ZipArchive(rewritten, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var entry in entries) { var target = output.CreateEntry(entry.Key); await using var stream = target.Open(); await stream.WriteAsync(entry.Value); }
        rewritten.Position = 0; await using var encrypted = new MemoryStream();
        await encryption.EncryptAsync(rewritten, encrypted, Password); return encrypted.ToArray();
    }

    private static async Task<bool> TableExistsAsync(string connectionString, string tableName)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = @name)", connection);
        command.Parameters.AddWithValue("name", tableName); return (bool)(await command.ExecuteScalarAsync())!;
    }

    private async Task ImportAsync(Guid tenantId, IReadOnlyDictionary<string, byte[]> files)
    {
        using var scope = ServiceProvider.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
        var importContext = new TenantBackupImportContext(tenantId, tenantId, TenantBackupRestoreMode.LocalClonePreserveIds);
        foreach (var contributor in scope.ServiceProvider.GetServices<ITenantBackupContributor>().OrderBy(x => x.Order))
            await contributor.ImportAsync(importContext, files);
        await uow.CompleteAsync();
    }

    private async Task DeleteCommercialDataAsync(Guid tenantId)
    {
        using var scope = ServiceProvider.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
        var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<NoxStockDbContext>>().GetDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("UPDATE \"AppDocumentItems\" SET \"ProductCostHistoryId\" = NULL WHERE \"TenantId\" = @p0", tenantId);
#pragma warning disable EF1002 // Table names come only from the hard-coded test whitelist above.
        foreach (var table in CommercialTablesInDeleteOrder)
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM \"{table}\" WHERE \"TenantId\" = @p0", tenantId);
#pragma warning restore EF1002
        await uow.CompleteAsync();
    }

    private async Task AddProcessingCommandAsync(Guid tenantId, Guid documentId)
    {
        using var scope = ServiceProvider.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
        var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<NoxStockDbContext>>().GetDbContextAsync();
        db.CommercialCommands.Add(new CommercialCommand(Guid.NewGuid(), tenantId, CommercialCommandType.DocumentApproval,
            "processing-command-must-not-export") { DocumentId = documentId });
        await db.SaveChangesAsync();
        await uow.CompleteAsync();
    }

    private async Task<RepresentativeIds> SeedRepresentativeTenantAsync(Guid tenantId, string suffix)
    {
        var ids = new RepresentativeIds();
        using var scope = ServiceProvider.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
        var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<NoxStockDbContext>>().GetDbContextAsync();

        var brand = new Brand(Guid.NewGuid(), tenantId, $"Brand {suffix}");
        var category = new Category(Guid.NewGuid(), tenantId, $"Category {suffix}");
        var product = new Product(ids.ProductId, tenantId, $"Product {suffix}", $"SKU-{suffix}")
        { BrandId = brand.Id, CategoryId = category.Id, Quantity = 7m, PurchasePrice = 10m, SalePrice = 20m, TrackStock = true,
            Unit = "Adet", Barcode = $"BAR-{suffix}", Description = $"Description {suffix}", ShelfLocation = "A-1" };
        var oem = new ProductOEMCode(Guid.NewGuid(), tenantId, product.Id, $"OEM-{suffix}");
        var customer = new BusinessPartner(Guid.NewGuid(), tenantId, $"Customer {suffix}", BusinessPartnerType.Customer) { Balance = 80m };
        var supplier = new BusinessPartner(Guid.NewGuid(), tenantId, $"Supplier {suffix}", BusinessPartnerType.Supplier) { Balance = -25m };
        var account = new Account(Guid.NewGuid(), tenantId, $"Cash {suffix}", AccountType.Cash) { Balance = 50m };
        var source = new Document(ids.SourceDocumentId, tenantId)
        {
            BusinessPartnerId = customer.Id, DocNumber = $"SF-{suffix}", Type = DocumentType.SaleInvoice,
            Status = DocumentStatus.Approved, Subtotal = 100m, VatTotal = 20m, GrandTotal = 120m, PaidAmount = 40m,
            Currency = "TRY", ExchangeRate = 1m, Date = DateTime.UtcNow
        };
        source.CapturePrintSnapshot(DateTime.UtcNow, $"Snapshot Company {suffix}", "Address", "555", "mail@example.test", "Tax Office", "123",
            $"Snapshot Customer {suffix}", "Customer Address", "Customer Tax", "999");
        var sourceItem = new DocumentItem(ids.SourceItemId, tenantId)
        { DocumentId = source.Id, ProductId = product.Id, Quantity = 5m, UnitPrice = 20m, VatRate = 20m, Total = 120m };
        sourceItem.CaptureProductSnapshot($"Snapshot Product {suffix}", $"SNAP-{suffix}", "Adet");
        var returned = new Document(ids.ReturnDocumentId, tenantId)
        {
            BusinessPartnerId = customer.Id, SourceDocumentId = source.Id, DocNumber = $"IADE-{suffix}", Type = DocumentType.SalesReturn,
            Status = DocumentStatus.Approved, Subtotal = 20m, VatTotal = 4m, GrandTotal = 24m, Currency = "TRY", ExchangeRate = 1m, Date = DateTime.UtcNow
        };
        var returnItem = new DocumentItem(ids.ReturnItemId, tenantId)
        { DocumentId = returned.Id, SourceDocumentItemId = sourceItem.Id, ProductId = product.Id, Quantity = 1m, UnitPrice = 20m, VatRate = 20m, Total = 24m };
        returnItem.CopyProductSnapshotFrom(sourceItem);
        var transaction = new AccountTransaction(ids.AccountTransactionId, tenantId, account.Id, AccountTransactionType.Collection, 40m, "TRY", DateTime.UtcNow, source.Id)
        { BusinessPartnerId = customer.Id, BalanceAfter = 50m };
        var allocation = new PaymentAllocation(Guid.NewGuid(), tenantId, transaction.Id, source.Id, customer.Id, 40m, "TRY", DateTime.UtcNow);
        var movement = new StockMovement(Guid.NewGuid(), tenantId, product.Id, StockMovementType.SalesInvoice, -5m, 7m, DateTime.UtcNow, source.Id);
        var count = new InventoryCount(Guid.NewGuid(), tenantId, $"SC-{suffix}", $"Count {suffix}", DateTime.UtcNow, "tester");
        var countItem = new InventoryCountItem(Guid.NewGuid(), tenantId, count.Id, product.Id)
        { ProductName = product.Name, Sku = product.Sku, Unit = "Adet", SystemQuantity = 7m, CountedQuantity = 7m };

        db.AddRange(brand, category, product, oem, customer, supplier, account, source, sourceItem, returned, returnItem,
            transaction, allocation, movement, count, countItem,
            new BusinessPartnerOpeningBalance(Guid.NewGuid(), tenantId, customer.Id, BusinessPartnerOpeningDirection.ReceivableFromPartner, 80m, "TRY", DateTime.UtcNow, $"OB-{suffix}"),
            new BusinessPartnerTransfer(Guid.NewGuid(), tenantId, customer.Id, supplier.Id, $"TR-{suffix}", 5m, "TRY", DateTime.UtcNow, null),
            new DocumentExchangeRateSnapshot(Guid.NewGuid(), tenantId, source.Id, "USD", 42m, DateTime.UtcNow),
            new DocumentNumberSequence(Guid.NewGuid(), tenantId, DocumentType.SaleInvoice, DateTime.UtcNow.Year, "SF", 41));
        await db.SaveChangesAsync();

        var cost = new ProductCostHistory(ids.CostHistoryId, tenantId)
        {
            ProductId = product.Id, SourceDocumentId = source.Id, SourceDocumentItemId = sourceItem.Id, SupplierId = supplier.Id,
            CostDate = DateTime.UtcNow, SourceNetUnitCost = 10m, SourceExchangeRate = 1m, TryEquivalentNetUnitCost = 10m,
            ReferenceCurrencyEquivalentCost = 10m, Quantity = 5m, QuantityBefore = 2m, QuantityAfter = 7m,
            AverageCostBeforeTry = 8m, AverageCostAfterTry = 10m, ChangeType = ProductCostHistoryChangeType.PurchaseApproved
        };
        db.ProductCostHistories.Add(cost);
        await db.SaveChangesAsync();
        sourceItem.ProductCostHistoryId = cost.Id;
        db.CommercialCommands.Add(new CommercialCommand(Guid.NewGuid(), tenantId, CommercialCommandType.DocumentApproval,
            $"completed-{suffix}") { DocumentId = source.Id });
        var command = db.CommercialCommands.Local.Last();
        command.Status = CommercialCommandStatus.Succeeded;
        command.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<ISettingManager>()
            .SetForTenantAsync(tenantId, NoxStockSettings.DefaultCashAccountId, account.Id.ToString());
        await uow.CompleteAsync();
        return ids;
    }

    private static void AssertInvariants(TenantBackupInvariantSummaryDto expected, TenantBackupInvariantSummary actual)
    {
        actual.Products.ShouldBe(expected.Products); actual.BusinessPartners.ShouldBe(expected.BusinessPartners);
        actual.Documents.ShouldBe(expected.Documents); actual.Accounts.ShouldBe(expected.Accounts);
        actual.ProductQuantity.ShouldBe(expected.ProductQuantity); actual.AccountBalance.ShouldBe(expected.AccountBalance);
        actual.PartnerBalance.ShouldBe(expected.PartnerBalance); actual.DocumentTotal.ShouldBe(expected.DocumentTotal);
    }

    private static async Task<byte[]> RewriteManifestAsync(IServiceProvider services, byte[] package, Action<TenantBackupManifest> rewrite)
    {
        var encryption = services.GetRequiredService<IBackupEncryptionService>();
        await using var zip = new MemoryStream();
        await encryption.DecryptAsync(new MemoryStream(package), zip, Password);
        zip.Position = 0;
        await using var rewrittenZip = new MemoryStream();
        using (var input = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true))
        using (var output = new ZipArchive(rewrittenZip, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in input.Entries)
            {
                var target = output.CreateEntry(entry.FullName);
                await using var targetStream = target.Open();
                if (entry.FullName == "manifest.json")
                {
                    TenantBackupManifest manifest;
                    await using (var source = entry.Open()) manifest = (await JsonSerializer.DeserializeAsync<TenantBackupManifest>(source, new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
                    rewrite(manifest);
                    await JsonSerializer.SerializeAsync(targetStream, manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                }
                else { await using var source = entry.Open(); await source.CopyToAsync(targetStream); }
            }
        }
        rewrittenZip.Position = 0;
        await using var encrypted = new MemoryStream();
        await encryption.EncryptAsync(rewrittenZip, encrypted, Password);
        return encrypted.ToArray();
    }

    private static async Task<byte[]> TamperPayloadWithoutUpdatingManifestAsync(IServiceProvider services, byte[] package)
    {
        var encryption = services.GetRequiredService<IBackupEncryptionService>();
        await using var zip = new MemoryStream();
        await encryption.DecryptAsync(new MemoryStream(package), zip, Password);
        zip.Position = 0;
        await using var rewrittenZip = new MemoryStream();
        using (var input = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true))
        using (var output = new ZipArchive(rewrittenZip, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in input.Entries)
            {
                var target = output.CreateEntry(entry.FullName);
                await using var targetStream = target.Open();
                await using var source = entry.Open();
                await source.CopyToAsync(targetStream);
                if (entry.FullName == "catalog/AppProducts.jsonl") await targetStream.WriteAsync(" "u8.ToArray());
            }
        }
        rewrittenZip.Position = 0;
        await using var encrypted = new MemoryStream();
        await encryption.EncryptAsync(rewrittenZip, encrypted, Password);
        return encrypted.ToArray();
    }

    private sealed class RepresentativeIds
    {
        public Guid ProductId { get; } = Guid.NewGuid();
        public Guid SourceDocumentId { get; } = Guid.NewGuid();
        public Guid SourceItemId { get; } = Guid.NewGuid();
        public Guid ReturnDocumentId { get; } = Guid.NewGuid();
        public Guid ReturnItemId { get; } = Guid.NewGuid();
        public Guid AccountTransactionId { get; } = Guid.NewGuid();
        public Guid CostHistoryId { get; } = Guid.NewGuid();
    }
}
