using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoxStock.MultiTenancy;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace NoxStock.TenantBackups;

public sealed class TenantCloneJobArgs
{
    public Guid OperationId { get; set; }
}

public sealed class TenantCloneJob : AsyncBackgroundJob<TenantCloneJobArgs>
{
    private readonly TenantCloneOperationExecutor _executor;

    public TenantCloneJob(TenantCloneOperationExecutor executor) => _executor = executor;

    public override Task ExecuteAsync(TenantCloneJobArgs args) => _executor.ExecuteAsync(args.OperationId);
}

/// <summary>
/// Executes the durable operation.  It intentionally catches failures so the
/// background-job provider cannot replay an import after its terminal state was
/// persisted.  A user starts a new operation for an explicit retry.
/// </summary>
public sealed class TenantCloneOperationExecutor : ITransientDependency
{
    private readonly IHostEnvironment _environment;
    private readonly IRepository<TenantCloneOperation, Guid> _operations;
    private readonly IRepository<TenantBackup, Guid> _backups;
    private readonly IDataFilter<IMultiTenant> _dataFilter;
    private readonly ITenantBackupStorage _storage;
    private readonly TenantBackupPackageService _packages;
    private readonly IEnumerable<ITenantBackupContributor> _contributors;
    private readonly ITenantBackupSnapshotStore _store;
    private readonly ITenantRepository _tenants;
    private readonly ITenantManager _tenantManager;
    private readonly Volo.Abp.Data.IDataSeeder _seeder;
    private readonly IUnitOfWorkManager _uow;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataProtector _protector;
    private readonly ILogger<TenantCloneOperationExecutor> _logger;

    public TenantCloneOperationExecutor(
        IHostEnvironment environment,
        IRepository<TenantCloneOperation, Guid> operations,
        IRepository<TenantBackup, Guid> backups,
        IDataFilter<IMultiTenant> dataFilter,
        ITenantBackupStorage storage,
        TenantBackupPackageService packages,
        IEnumerable<ITenantBackupContributor> contributors,
        ITenantBackupSnapshotStore store,
        ITenantRepository tenants,
        ITenantManager tenantManager,
        Volo.Abp.Data.IDataSeeder seeder,
        IUnitOfWorkManager uow,
        ICurrentTenant currentTenant,
        IDataProtectionProvider protection,
        ILogger<TenantCloneOperationExecutor> logger)
    {
        _environment = environment;
        _operations = operations;
        _backups = backups;
        _dataFilter = dataFilter;
        _storage = storage;
        _packages = packages;
        _contributors = contributors;
        _store = store;
        _tenants = tenants;
        _tenantManager = tenantManager;
        _seeder = seeder;
        _uow = uow;
        _currentTenant = currentTenant;
        _protector = protection.CreateProtector("NoxStock.TenantClone.v1");
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid operationId)
    {
        TenantCloneOperation operation;
        try
        {
            operation = await _operations.GetAsync(operationId);
            if (operation.Status != TenantCloneOperationStatus.Queued || !_environment.IsDevelopment()) return;
            await MarkProgressAsync(operationId, TenantCloneOperationStatus.Validating, 0);

            TenantBackup source;
            using (_dataFilter.Disable())
            {
                source = await _backups.FindAsync(operation.SourceBackupId)
                    ?? throw new BusinessException("NoxStock:BackupCloneSourceUnavailable");
            }
            EnsureSourceAvailable(source);

            Stream package;
            using (_currentTenant.Change(source.TenantId))
                package = await _storage.OpenReadAsync(source.StorageName!);
            await using (package)
            {
            TenantBackupManifest manifest;
            IReadOnlyDictionary<string, byte[]> files;
            using (var validation = _uow.Begin(requiresNew: true, isTransactional: false))
            {
                (manifest, files) = await _packages.ReadAndValidateAsync(package, _protector.Unprotect(operation.ProtectedPackagePassword));
                await validation.CompleteAsync();
            }
            if (manifest.SourceTenantId != source.TenantId)
                throw new BusinessException("NoxStock:BackupCloneSourceUnavailable");

            await EnsureTargetAvailableAsync(operation.TargetTenantName, operation.TargetTenantCode);
            var masterRecords = CountRecords(files, "tenant-settings", "catalog", "parties-accounts");
            var totalRecords = files.Values.Sum(content => System.Text.Encoding.UTF8.GetString(content)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries).LongLength);
            await MarkProgressAsync(operationId, TenantCloneOperationStatus.CreatingTenant, 0);

            Guid targetTenantId;
            using (var import = _uow.Begin(requiresNew: true, isTransactional: true))
            {
                var tenant = await _tenantManager.CreateAsync(operation.TargetTenantCode);
                tenant.SetNoxStockActive(false);
                tenant.SetProperty("NoxStockCloneDisplayName", operation.TargetTenantName);
                await _tenants.InsertAsync(tenant, autoSave: true);
                targetTenantId = tenant.Id;

                using (_currentTenant.Change(targetTenantId, tenant.Name))
                {
                    var context = new TenantBackupImportContext(manifest.SourceTenantId, targetTenantId,
                        TenantBackupRestoreMode.NewTenantCloneRemapIds);
                    await MarkProgressAsync(operationId, TenantCloneOperationStatus.ImportingMasterData, masterRecords);
                    foreach (var contributor in _contributors.OrderBy(x => x.Order).Where(x => x.Order <= 30))
                        await contributor.ImportAsync(context, files);

                    await MarkProgressAsync(operationId, TenantCloneOperationStatus.ImportingCommercialData, totalRecords);
                    foreach (var contributor in _contributors.OrderBy(x => x.Order).Where(x => x.Order > 30))
                        await contributor.ImportAsync(context, files);

                    await MarkProgressAsync(operationId, TenantCloneOperationStatus.Verifying, totalRecords);
                    var actual = await _store.GetInvariantSummaryAsync(targetTenantId);
                    EnsureInvariants(manifest.Invariants, actual);
                    await _seeder.SeedAsync(new DataSeedContext(targetTenantId)
                        .WithProperty("AdminEmail", operation.AdminEmailAddress)
                        .WithProperty("AdminPassword", _protector.Unprotect(operation.ProtectedAdminPassword)));
                }

                tenant.SetNoxStockActive(true);
                await _tenants.UpdateAsync(tenant, autoSave: true);
                await import.CompleteAsync();
            }

            await MarkSucceededAsync(operationId, targetTenantId, totalRecords);
            }
        }
        catch (BusinessException exception)
        {
            await MarkFailedSafelyAsync(operationId, exception.Code ?? "NoxStock:BackupCloneFailed");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Tenant clone operation {OperationId} failed", operationId);
            await MarkFailedSafelyAsync(operationId, "NoxStock:BackupCloneFailed");
        }
    }

    private async Task MarkProgressAsync(Guid id, TenantCloneOperationStatus status, long count)
    {
        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var operation = await _operations.GetAsync(id);
        operation.MarkProgress(status, count);
        await _operations.UpdateAsync(operation, autoSave: true);
        await uow.CompleteAsync();
    }

    private async Task MarkSucceededAsync(Guid id, Guid tenantId, long count)
    {
        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var operation = await _operations.GetAsync(id);
        operation.MarkSucceeded(tenantId, count, DateTime.UtcNow);
        await _operations.UpdateAsync(operation, autoSave: true);
        await uow.CompleteAsync();
    }

    private async Task MarkFailedSafelyAsync(Guid id, string code)
    {
        try
        {
            using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
            var operation = await _operations.FindAsync(id);
            if (operation is not null)
            {
                operation.MarkFailed(code, DateTime.UtcNow);
                await _operations.UpdateAsync(operation, autoSave: true);
            }
            await uow.CompleteAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not persist terminal state for tenant clone operation {OperationId}", id);
        }
    }

    private async Task EnsureTargetAvailableAsync(string targetName, string targetCode)
    {
        if (await _tenants.FindByNameAsync(targetCode.ToUpperInvariant()) is not null)
            throw new BusinessException("NoxStock:BackupRestoreTargetExists");
        var tenants = await _tenants.GetListAsync();
        if (tenants.Any(x => string.Equals(x.GetProperty<string>("NoxStockCloneDisplayName"), targetName, StringComparison.OrdinalIgnoreCase)))
            throw new BusinessException("NoxStock:BackupRestoreTargetExists");
    }

    private static long CountRecords(IReadOnlyDictionary<string, byte[]> files, params string[] moduleNames) =>
        files.Where(x => moduleNames.Any(module => x.Key.StartsWith(module + "/", StringComparison.Ordinal)))
            .Sum(x => System.Text.Encoding.UTF8.GetString(x.Value).Split('\n', StringSplitOptions.RemoveEmptyEntries).LongLength);

    private static void EnsureSourceAvailable(TenantBackup backup)
    {
        if (backup.Status != TenantBackupStatus.Succeeded || backup.ExpiresAtUtc <= DateTime.UtcNow ||
            string.IsNullOrWhiteSpace(backup.StorageName) || backup.FileSize is > TenantBackupPackageService.MaxPackageSizeBytes)
            throw new BusinessException("NoxStock:BackupCloneSourceUnavailable");
    }

    private static void EnsureInvariants(TenantBackupInvariantSummaryDto expected, TenantBackupInvariantSummary actual)
    {
        if (expected.Products != actual.Products || expected.BusinessPartners != actual.BusinessPartners || expected.Documents != actual.Documents ||
            expected.Accounts != actual.Accounts || expected.ProductQuantity != actual.ProductQuantity || expected.AccountBalance != actual.AccountBalance ||
            expected.PartnerBalance != actual.PartnerBalance || expected.DocumentTotal != actual.DocumentTotal)
            throw new BusinessException("NoxStock:BackupInvariantMismatch");
    }
}
