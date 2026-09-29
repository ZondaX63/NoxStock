using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoxStock.MultiTenancy;
using NoxStock.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Content;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Volo.Abp.Auditing;

namespace NoxStock.TenantBackups;

[Authorize(NoxStockPermissions.TenantBackups.Default)]
public class TenantBackupAppService : NoxStockAppService, ITenantBackupAppService
{
    private readonly IRepository<TenantBackup, Guid> _repository;
    private readonly IBackgroundJobManager _jobs;
    private readonly ITenantBackupStorage _storage;
    private readonly IDataProtector _protector;

    public TenantBackupAppService(IRepository<TenantBackup, Guid> repository, IBackgroundJobManager jobs,
        ITenantBackupStorage storage, IDataProtectionProvider protection)
    {
        _repository = repository; _jobs = jobs; _storage = storage;
        _protector = protection.CreateProtector("NoxStock.TenantBackup.v1");
    }

    [Authorize(NoxStockPermissions.TenantBackups.Create)]
    public async Task<TenantBackupDto> CreateAsync(CreateTenantBackupInputDto input)
    {
        var tenantId = CurrentTenant.Id ?? throw new BusinessException("NoxStock:TenantRequired");
        var backup = new TenantBackup(GuidGenerator.Create(), tenantId, CurrentUser.Id, Clock.Now.ToUniversalTime(), Clock.Now.AddDays(7).ToUniversalTime());
        await _repository.InsertAsync(backup, autoSave: true);
        await _jobs.EnqueueAsync(new TenantBackupJobArgs
        {
            BackupId = backup.Id, TenantId = tenantId,
            ProtectedPassword = _protector.Protect(input.Password)
        });
        return Map(backup);
    }

    public async Task<PagedResultDto<TenantBackupDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        query = query.OrderByDescending(x => x.RequestedAtUtc).Skip(input.SkipCount).Take(input.MaxResultCount);
        var items = await AsyncExecuter.ToListAsync(query);
        return new PagedResultDto<TenantBackupDto>(await _repository.GetCountAsync(), items.Select(Map).ToList());
    }

    [Authorize(NoxStockPermissions.TenantBackups.Download)]
    public async Task<TenantBackupDownloadTokenDto> GetDownloadTokenAsync(Guid id)
    {
        var backup = await _repository.GetAsync(id);
        EnsureDownloadable(backup);
        var expires = Clock.Now.AddMinutes(5).ToUniversalTime();
        return new TenantBackupDownloadTokenDto { ExpiresAtUtc = expires, Token = _protector.Protect(JsonSerializer.Serialize(new DownloadClaim(id, backup.TenantId, expires))) };
    }

    [Authorize(NoxStockPermissions.TenantBackups.Download)]
    public async Task<IRemoteStreamContent> DownloadAsync(Guid id, string token)
    {
        DownloadClaim claim;
        try { claim = JsonSerializer.Deserialize<DownloadClaim>(_protector.Unprotect(token))!; }
        catch { throw new BusinessException("NoxStock:BackupDownloadTokenInvalid"); }
        if (claim is null || claim.Id != id || claim.ExpiresAtUtc <= Clock.Now.ToUniversalTime()) throw new BusinessException("NoxStock:BackupDownloadTokenInvalid");
        var backup = await _repository.GetAsync(id);
        if (claim.TenantId != backup.TenantId) throw new BusinessException("NoxStock:BackupDownloadTokenInvalid");
        EnsureDownloadable(backup);
        return new RemoteStreamContent(await _storage.OpenReadAsync(backup.StorageName!), $"noxstock-{backup.RequestedAtUtc:yyyyMMdd-HHmmss}.noxbackup", "application/octet-stream");
    }

    [Authorize(NoxStockPermissions.TenantBackups.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var backup = await _repository.GetAsync(id);
        if (!string.IsNullOrWhiteSpace(backup.StorageName)) await _storage.DeleteAsync(backup.StorageName);
        await _repository.DeleteAsync(backup);
    }

    private void EnsureDownloadable(TenantBackup backup)
    {
        if (backup.Status != TenantBackupStatus.Succeeded || backup.ExpiresAtUtc <= Clock.Now.ToUniversalTime() || string.IsNullOrWhiteSpace(backup.StorageName))
            throw new BusinessException("NoxStock:BackupNotDownloadable");
    }

    private static TenantBackupDto Map(TenantBackup x) => new() { Id = x.Id, TenantId = x.TenantId, Status = x.Status, RequestedBy = x.RequestedBy,
        RequestedAtUtc = x.RequestedAtUtc, CompletedAtUtc = x.CompletedAtUtc, FileSize = x.FileSize, ChecksumSha256 = x.ChecksumSha256,
        ExpiresAtUtc = x.ExpiresAtUtc, FailureCode = x.FailureCode };
    private sealed record DownloadClaim(Guid Id, Guid? TenantId, DateTime ExpiresAtUtc);
}

public sealed class TenantBackupJobArgs
{
    public Guid BackupId { get; set; }
    public Guid TenantId { get; set; }
    public string ProtectedPassword { get; set; } = string.Empty;
}

public sealed class TenantBackupJob : AsyncBackgroundJob<TenantBackupJobArgs>
{
    private readonly IRepository<TenantBackup, Guid> _repository;
    private readonly ITenantRepository _tenants;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _uow;
    private readonly TenantBackupPackageService _packages;
    private readonly ITenantBackupStorage _storage;
    private readonly IDataProtector _protector;
    private readonly ILogger<TenantBackupJob> _logger;

    public TenantBackupJob(IRepository<TenantBackup, Guid> repository, ITenantRepository tenants, ICurrentTenant currentTenant,
        IUnitOfWorkManager uow, TenantBackupPackageService packages, ITenantBackupStorage storage,
        IDataProtectionProvider protection, ILogger<TenantBackupJob> logger)
    {
        _repository = repository; _tenants = tenants; _currentTenant = currentTenant; _uow = uow; _packages = packages; _storage = storage;
        _protector = protection.CreateProtector("NoxStock.TenantBackup.v1"); _logger = logger;
    }

    public override async Task ExecuteAsync(TenantBackupJobArgs args)
    {
        var storageName = $"{args.TenantId:N}/{args.BackupId:N}.noxbackup";
        var stored = false;
        try
        {
            string tenantName;
            using (_currentTenant.Change(args.TenantId))
            {
                var backup = await _repository.GetAsync(args.BackupId);
                backup.MarkProcessing();
                await _repository.UpdateAsync(backup, autoSave: true);
            }
            var tenant = await _tenants.GetAsync(args.TenantId);
            tenantName = tenant.Name;
            MemoryStream package;
            using (_currentTenant.Change(args.TenantId, tenantName))
            using (var snapshot = _uow.Begin(requiresNew: true, isTransactional: true, isolationLevel: System.Data.IsolationLevel.RepeatableRead))
            {
                package = await _packages.CreateAsync(args.TenantId, tenantName, _protector.Unprotect(args.ProtectedPassword));
                await snapshot.CompleteAsync();
            }
            await using (package)
            {
            var checksum = Convert.ToHexString(SHA256.HashData(package.ToArray())).ToLowerInvariant();
            using (_currentTenant.Change(args.TenantId))
                await _storage.SaveAsync(storageName, package);
            stored = true;
            using (_currentTenant.Change(args.TenantId))
            {
                var backup = await _repository.GetAsync(args.BackupId);
                backup.MarkSucceeded(DateTime.UtcNow, package.Length, checksum, storageName);
                await _repository.UpdateAsync(backup, autoSave: true);
            }
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Tenant backup job failed");
            if (stored)
            {
                try
                {
                    using (_currentTenant.Change(args.TenantId))
                        await _storage.DeleteAsync(storageName);
                }
                catch (Exception cleanupException) { _logger.LogWarning(cleanupException, "Tenant backup orphan cleanup failed"); }
            }
            using (_currentTenant.Change(args.TenantId))
            {
                var backup = await _repository.FindAsync(args.BackupId);
                if (backup is not null) { backup.MarkFailed(DateTime.UtcNow, "NoxStock:BackupGenerationFailed"); await _repository.UpdateAsync(backup, autoSave: true); }
            }
        }
    }
}

[Authorize(NoxStockPermissions.TenantBackups.Restore)]
public class TenantRestoreAppService : NoxStockAppService, ITenantRestoreAppService
{
    private readonly IHostEnvironment _environment;
    private readonly TenantBackupPackageService _packages;
    private readonly IEnumerable<ITenantBackupContributor> _contributors;
    private readonly ITenantBackupSnapshotStore _store;
    private readonly ITenantRepository _tenants;
    private readonly ITenantManager _tenantManager;
    private readonly IDataSeeder _seeder;
    private readonly IUnitOfWorkManager _uow;
    private readonly IRepository<TenantCloneOperation, Guid> _cloneOperations;
    private readonly IRepository<TenantBackup, Guid> _backupRepository;
    private readonly IDataFilter<IMultiTenant> _dataFilter;
    private readonly IBackgroundJobManager _jobs;
    private readonly IDataProtector _cloneProtector;

    public TenantRestoreAppService(IHostEnvironment environment, TenantBackupPackageService packages, IEnumerable<ITenantBackupContributor> contributors,
        ITenantBackupSnapshotStore store, ITenantRepository tenants, ITenantManager tenantManager, IDataSeeder seeder, IUnitOfWorkManager uow,
        IRepository<TenantCloneOperation, Guid> cloneOperations, IRepository<TenantBackup, Guid> backupRepository,
        IDataFilter<IMultiTenant> dataFilter, IBackgroundJobManager jobs, IDataProtectionProvider protection)
    { _environment = environment; _packages = packages; _contributors = contributors; _store = store; _tenants = tenants;
        _tenantManager = tenantManager; _seeder = seeder; _uow = uow; _cloneOperations = cloneOperations;
        _backupRepository = backupRepository; _dataFilter = dataFilter; _jobs = jobs;
        _cloneProtector = protection.CreateProtector("NoxStock.TenantClone.v1"); }

    public async Task<TenantBackupValidationDto> ValidateAsync(ValidateTenantBackupInputDto input)
    {
        EnsureLocalHost();
        await using var stream = input.Package.GetStream();
        var (manifest, _) = await _packages.ReadAndValidateAsync(stream, input.Password);
        return new TenantBackupValidationDto { IsValid = true, FormatVersion = manifest.FormatVersion, SourceTenantId = manifest.SourceTenantId,
            SourceTenantName = manifest.SourceTenantName, CreatedAtUtc = manifest.CreatedAtUtc, SchemaVersion = manifest.SchemaVersion, Modules = manifest.ExportedModules };
    }

    public async Task<TenantBackupRestorePreviewDto> PreviewAsync(PreviewTenantBackupRestoreInputDto input)
    {
        EnsureLocalHost();
        await using var stream = input.Package.GetStream();
        var (manifest, _) = await _packages.ReadAndValidateAsync(stream, input.Password);
        var targetCode = TargetCode(input);
        await EnsureTargetAvailableAsync(input.RestoreMode, manifest, input.TargetTenantName, targetCode);
        return new TenantBackupRestorePreviewDto
        {
            RestoreMode = input.RestoreMode,
            SourceTenantId = manifest.SourceTenantId,
            SourceTenantName = manifest.SourceTenantName,
            TargetTenantId = input.RestoreMode == TenantBackupRestoreMode.LocalClonePreserveIds ? manifest.SourceTenantId : null,
            TargetTenantName = input.TargetTenantName,
            TargetTenantCode = targetCode,
            IsCompatible = true,
            ModuleRecordCounts = manifest.ExportedModules.ToDictionary(
                module => module,
                module => manifest.Files.Where(x => x.Name.StartsWith(module + "/", StringComparison.Ordinal)).Sum(x => x.RecordCount),
                StringComparer.Ordinal),
            Warnings =
            [
                "Kullanıcı parolaları, tokenlar, lisanslar ve secrets clone edilmez.",
                "Tarihsel belge, kur, fiyat, maliyet ve print snapshot değerleri paketteki haliyle korunur."
            ]
        };
    }

    [Audited]
    public async Task<TenantBackupRestoreResultDto> RestoreToLocalOrNewTenantAsync(RestoreTenantBackupInputDto input)
    {
        EnsureLocalHost();
        await using var stream = input.Package.GetStream();
        var (manifest, files) = await _packages.ReadAndValidateAsync(stream, input.Password);
        var targetCode = TargetCode(input);
        await EnsureTargetAvailableAsync(input.RestoreMode, manifest, input.TargetTenantName, targetCode);

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var tenant = input.RestoreMode == TenantBackupRestoreMode.LocalClonePreserveIds
            ? CreateTenantWithPreservedId(manifest.SourceTenantId, targetCode)
            : await _tenantManager.CreateAsync(targetCode);
        tenant.SetNoxStockActive(true);
        tenant.SetProperty("NoxStockCloneDisplayName", input.TargetTenantName);
        await _tenants.InsertAsync(tenant, autoSave: true);
        using (CurrentTenant.Change(tenant.Id, tenant.Name))
        {
            if (!await _store.IsTenantDataEmptyAsync(tenant.Id)) throw new BusinessException("NoxStock:BackupRestoreTargetNotEmpty");
            var importContext = new TenantBackupImportContext(manifest.SourceTenantId, tenant.Id, input.RestoreMode);
            foreach (var contributor in _contributors.OrderBy(x => x.Order)) await contributor.ImportAsync(importContext, files);
            await _seeder.SeedAsync(new DataSeedContext(tenant.Id).WithProperty("AdminEmail", input.AdminEmailAddress).WithProperty("AdminPassword", input.AdminPassword));
            var actual = await _store.GetInvariantSummaryAsync(tenant.Id);
            EnsureInvariants(manifest.Invariants, actual);
            await uow.CompleteAsync();
            return new TenantBackupRestoreResultDto { TenantId = tenant.Id, TenantName = input.TargetTenantName,
                TenantCode = tenant.Name, RestoreMode = input.RestoreMode, Invariants = Map(actual) };
        }
    }

    public async Task<TenantCloneOperationDto> StartCloneAsync(StartTenantCloneInputDto input)
    {
        EnsureLocalHost();
        if (input.SourceBackupId == Guid.Empty || string.IsNullOrWhiteSpace(input.OperationKey) ||
            string.IsNullOrWhiteSpace(input.Password) || input.Password.Length < 12)
            throw new BusinessException("NoxStock:BackupCloneInputInvalid");

        var targetCode = input.TargetTenantCode.Trim();
        var targetName = input.TargetTenantName.Trim();
        var operationKey = input.OperationKey.Trim();
        await EnsureCloneSourceAvailableAsync(input.SourceBackupId);
        var existing = await FindDuplicateCloneAsync(input.SourceBackupId, targetCode, targetName, operationKey);
        if (existing is not null) return Map(existing);

        var operation = new TenantCloneOperation(GuidGenerator.Create(), input.SourceBackupId, targetName, targetCode,
            operationKey, _cloneProtector.Protect(input.Password), input.AdminEmailAddress,
            _cloneProtector.Protect(input.AdminPassword), Clock.Now.ToUniversalTime());
        try { await _cloneOperations.InsertAsync(operation, autoSave: true); }
        catch (Exception)
        {
            var duplicate = await FindDuplicateCloneAsync(input.SourceBackupId, targetCode, targetName, operationKey);
            if (duplicate is not null) return Map(duplicate);
            throw new BusinessException("NoxStock:BackupCloneAlreadyRequested");
        }

        await _jobs.EnqueueAsync(new TenantCloneJobArgs { OperationId = operation.Id });
        return Map(operation);
    }

    public async Task<TenantCloneOperationDto> GetCloneOperationAsync(Guid id)
    {
        EnsureLocalHost();
        return Map(await _cloneOperations.GetAsync(id));
    }

    public async Task<TenantCloneOperationDto> CancelCloneAsync(Guid id)
    {
        EnsureLocalHost();
        var operation = await _cloneOperations.GetAsync(id);
        try { operation.Cancel(Clock.Now.ToUniversalTime()); }
        catch (InvalidOperationException) { throw new BusinessException("NoxStock:BackupCloneCannotCancel"); }
        await _cloneOperations.UpdateAsync(operation, autoSave: true);
        return Map(operation);
    }

    public async Task<IReadOnlyList<TenantBackupDto>> GetCloneSourcesAsync()
    {
        EnsureLocalHost();
        using (_dataFilter.Disable())
        {
            var query = await _backupRepository.GetQueryableAsync();
            var backups = await AsyncExecuter.ToListAsync(query
                .Where(x => x.Status == TenantBackupStatus.Succeeded && x.ExpiresAtUtc > Clock.Now.ToUniversalTime() && x.StorageName != null)
                .OrderByDescending(x => x.RequestedAtUtc).Take(100));
            return backups.Select(TenantBackupMap).ToList();
        }
    }

    private async Task EnsureTargetAvailableAsync(TenantBackupRestoreMode mode, TenantBackupManifest manifest,
        string targetName, string targetCode)
    {
        if (!Enum.IsDefined(mode)) throw new BusinessException("NoxStock:BackupRestoreModeInvalid");
        if (mode == TenantBackupRestoreMode.LocalClonePreserveIds && await _tenants.FindAsync(manifest.SourceTenantId) is not null)
            throw new BusinessException("NoxStock:BackupRestoreTargetExists");
        if (await _tenants.FindByNameAsync(targetCode.ToUpperInvariant()) is not null)
            throw new BusinessException("NoxStock:BackupRestoreTargetExists");
        var tenants = await _tenants.GetListAsync();
        if (tenants.Any(x => string.Equals(x.GetProperty<string>("NoxStockCloneDisplayName"), targetName, StringComparison.OrdinalIgnoreCase)))
            throw new BusinessException("NoxStock:BackupRestoreTargetExists");
    }

    private async Task EnsureCloneSourceAvailableAsync(Guid id)
    {
        using (_dataFilter.Disable())
        {
            var backup = await _backupRepository.FindAsync(id);
            if (backup is null || backup.Status != TenantBackupStatus.Succeeded || backup.ExpiresAtUtc <= Clock.Now.ToUniversalTime() ||
                string.IsNullOrWhiteSpace(backup.StorageName) || backup.FileSize is > TenantBackupPackageService.MaxPackageSizeBytes)
                throw new BusinessException("NoxStock:BackupCloneSourceUnavailable");
        }
    }

    private async Task<TenantCloneOperation?> FindDuplicateCloneAsync(Guid sourceBackupId, string targetCode, string targetName, string operationKey)
    {
        var query = await _cloneOperations.GetQueryableAsync();
        return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.OperationKey == operationKey ||
            (x.SourceBackupId == sourceBackupId && x.TargetTenantCode == targetCode && x.TargetTenantName == targetName &&
                (int)x.Status >= (int)TenantCloneOperationStatus.Queued && (int)x.Status <= (int)TenantCloneOperationStatus.Verifying))
            .OrderByDescending(x => x.RequestedAtUtc));
    }

    private static string TargetCode(PreviewTenantBackupRestoreInputDto input) =>
        string.IsNullOrWhiteSpace(input.TargetTenantCode) ? input.TargetTenantName.Trim() : input.TargetTenantCode.Trim();

    private void EnsureLocalHost()
    {
        if (!_environment.IsDevelopment() || CurrentTenant.Id is not null) throw new BusinessException("NoxStock:BackupRestoreLocalHostOnly");
    }
    private static void EnsureInvariants(TenantBackupInvariantSummaryDto expected, TenantBackupInvariantSummary actual)
    {
        if (expected.Products != actual.Products || expected.BusinessPartners != actual.BusinessPartners || expected.Documents != actual.Documents ||
            expected.Accounts != actual.Accounts || expected.ProductQuantity != actual.ProductQuantity || expected.AccountBalance != actual.AccountBalance ||
            expected.PartnerBalance != actual.PartnerBalance || expected.DocumentTotal != actual.DocumentTotal)
            throw new BusinessException("NoxStock:BackupInvariantMismatch");
    }
    private static TenantBackupInvariantSummaryDto Map(TenantBackupInvariantSummary x) => new() { Products = x.Products, BusinessPartners = x.BusinessPartners,
        Documents = x.Documents, Accounts = x.Accounts, ProductQuantity = x.ProductQuantity, AccountBalance = x.AccountBalance,
        PartnerBalance = x.PartnerBalance, DocumentTotal = x.DocumentTotal };

    private static TenantBackupDto TenantBackupMap(TenantBackup x) => new() { Id = x.Id, TenantId = x.TenantId, Status = x.Status,
        RequestedBy = x.RequestedBy, RequestedAtUtc = x.RequestedAtUtc, CompletedAtUtc = x.CompletedAtUtc, FileSize = x.FileSize,
        ChecksumSha256 = x.ChecksumSha256, ExpiresAtUtc = x.ExpiresAtUtc, FailureCode = x.FailureCode };
    private static TenantCloneOperationDto Map(TenantCloneOperation x) => new() { Id = x.Id, SourceBackupId = x.SourceBackupId,
        TargetTenantName = x.TargetTenantName, TargetTenantCode = x.TargetTenantCode, Status = x.Status,
        ProcessedRecordCount = x.ProcessedRecordCount, TargetTenantId = x.TargetTenantId, FailureCode = x.FailureCode,
        RequestedAtUtc = x.RequestedAtUtc, CompletedAtUtc = x.CompletedAtUtc };

    private static Tenant CreateTenantWithPreservedId(Guid id, string name)
    {
        var tenant = Activator.CreateInstance(typeof(Tenant), BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, args: [id, name, name.ToUpperInvariant()], culture: null) as Tenant;
        return tenant ?? throw new BusinessException("NoxStock:BackupSourceTenantIdCannotBePreserved");
    }
}
