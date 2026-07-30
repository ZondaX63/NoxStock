using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.MultiTenancy;
using NoxStock.Permissions;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.TenantManagement;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace NoxStock.Licensing;

[Authorize(NoxStockPermissions.Licenses.Default)]
public class HostLicenseAppService : NoxStockAppService, IHostLicenseAppService
{
    private readonly IRepository<LicensePlan, Guid> _planRepository;
    private readonly IRepository<TenantLicense, Guid> _licenseRepository;
    private readonly IRepository<TenantLicenseFeature, Guid> _featureRepository;
    private readonly IRepository<TenantLicenseLimit, Guid> _limitRepository;
    private readonly IRepository<TenantLicenseAuditLog, Guid> _auditRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantLicenseAccessChecker _accessChecker;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IClock _clock;

    public HostLicenseAppService(
        IRepository<LicensePlan, Guid> planRepository,
        IRepository<TenantLicense, Guid> licenseRepository,
        IRepository<TenantLicenseFeature, Guid> featureRepository,
        IRepository<TenantLicenseLimit, Guid> limitRepository,
        IRepository<TenantLicenseAuditLog, Guid> auditRepository,
        ITenantRepository tenantRepository,
        ITenantLicenseAccessChecker accessChecker,
        IUnitOfWorkManager unitOfWorkManager,
        IClock clock)
    {
        _planRepository = planRepository;
        _licenseRepository = licenseRepository;
        _featureRepository = featureRepository;
        _limitRepository = limitRepository;
        _auditRepository = auditRepository;
        _tenantRepository = tenantRepository;
        _accessChecker = accessChecker;
        _unitOfWorkManager = unitOfWorkManager;
        _clock = clock;
    }

    public async Task<List<HostTenantLicenseRowDto>> GetOverviewAsync()
    {
        EnsureHost();
        var tenants = await _tenantRepository.GetListAsync();
        var licenses = await _licenseRepository.GetListAsync(item => item.IsCurrent);
        var plans = await _planRepository.GetListAsync();
        var result = new List<HostTenantLicenseRowDto>();

        foreach (var tenant in tenants.OrderBy(item => item.Name))
        {
            var license = licenses.FirstOrDefault(item => item.TenantId == tenant.Id);
            result.Add(new HostTenantLicenseRowDto
            {
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                TenantIsActive = tenant.IsNoxStockActive(),
                License = license is null
                    ? null
                    : await MapLicenseAsync(
                        license,
                        tenant.Name,
                        plans.First(item => item.Id == license.LicensePlanId))
            });
        }

        return result;
    }

    public async Task<List<LicensePlanDto>> GetPlansAsync()
    {
        EnsureHost();
        return (await _planRepository.GetListAsync())
            .OrderBy(item => item.Name)
            .Select(MapPlan)
            .ToList();
    }

    public async Task<TenantLicenseDto> GetAsync(Guid id)
    {
        EnsureHost();
        var license = await _licenseRepository.GetAsync(id);
        var tenant = await _tenantRepository.GetAsync(license.TenantId);
        var plan = await _planRepository.GetAsync(license.LicensePlanId);
        return await MapLicenseAsync(license, tenant.Name, plan);
    }

    [Authorize(NoxStockPermissions.Licenses.ViewAudit)]
    public async Task<List<TenantLicenseAuditLogDto>> GetAuditLogAsync(Guid tenantLicenseId)
    {
        EnsureHost();
        return (await _auditRepository.GetListAsync(
                item => item.TenantLicenseId == tenantLicenseId))
            .OrderByDescending(item => item.CreationTime)
            .Select(item => new TenantLicenseAuditLogDto
            {
                Id = item.Id,
                TenantLicenseId = item.TenantLicenseId,
                TenantId = item.TenantId,
                Action = item.Action,
                PreviousStatus = item.PreviousStatus,
                NewStatus = item.NewStatus,
                Details = item.Details,
                CreationTime = item.CreationTime,
                CreatorId = item.CreatorId
            })
            .ToList();
    }

    [Authorize(NoxStockPermissions.Licenses.Manage)]
    [UnitOfWork(isTransactional: true)]
    public async Task<TenantLicenseDto> AssignAsync(AssignTenantLicenseDto input)
    {
        EnsureHost();
        await _tenantRepository.GetAsync(input.TenantId);
        var plan = await _planRepository.GetAsync(input.LicensePlanId);
        var existing = await _licenseRepository.FirstOrDefaultAsync(
            item => item.TenantId == input.TenantId && item.IsCurrent);
        if (existing is not null)
        {
            throw new BusinessException("NoxStock:TenantAlreadyHasCurrentLicense");
        }

        var license = new TenantLicense(
            GuidGenerator.Create(),
            input.TenantId,
            input.LicensePlanId,
            input.Status,
            input.StartsAtUtc,
            input.ExpiresAtUtc,
            input.GracePeriodEndsAtUtc,
            input.LicenseReference,
            input.IsAutoRenew,
            input.InternalNote);
        await _licenseRepository.InsertAsync(license, autoSave: true);
        await ReplaceEntitlementsAsync(license.Id, plan.Code);
        await AddAuditAsync(
            license,
            LicenseAuditActionNames.Created,
            null,
            license.Status,
            $"Plan: {plan.Code}");
        await RefreshAccessAsync(license.TenantId);
        return await GetAsync(license.Id);
    }

    [Authorize(NoxStockPermissions.Licenses.Manage)]
    [UnitOfWork(isTransactional: true)]
    public async Task<TenantLicenseDto> ChangePlanAsync(Guid id, ChangeTenantLicensePlanDto input)
    {
        EnsureHost();
        var license = await _licenseRepository.GetAsync(id);
        var plan = await _planRepository.GetAsync(input.LicensePlanId);
        var previousPlanId = license.LicensePlanId;
        license.ChangePlan(plan.Id, input.InternalNote);
        await _licenseRepository.UpdateAsync(license, autoSave: true);
        await ReplaceEntitlementsAsync(license.Id, plan.Code);
        await AddAuditAsync(
            license,
            LicenseAuditActionNames.PlanChanged,
            license.Status,
            license.Status,
            $"{previousPlanId} -> {plan.Code}");
        await RefreshAccessAsync(license.TenantId);
        return await GetAsync(id);
    }

    [Authorize(NoxStockPermissions.Licenses.Manage)]
    [UnitOfWork(isTransactional: true)]
    public async Task<TenantLicenseDto> UpdateEntitlementsAsync(
        Guid id,
        UpdateTenantLicenseEntitlementsDto input)
    {
        EnsureHost();
        var license = await _licenseRepository.GetAsync(id);
        var plan = await _planRepository.GetAsync(license.LicensePlanId);
        if (!plan.IsCustom)
        {
            throw new BusinessException("NoxStock:CustomLicenseRequired");
        }

        var enabledFeatures = input.EnabledFeatures
            .Where(feature => !string.IsNullOrWhiteSpace(feature))
            .Select(feature => feature.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (enabledFeatures.Any(feature => !LicenseFeatureNames.All.Contains(
                feature,
                StringComparer.OrdinalIgnoreCase)))
        {
            throw new BusinessException("NoxStock:UnknownLicenseFeature");
        }

        var existingFeatures = (await _featureRepository.GetListAsync(
                item => item.TenantLicenseId == license.Id))
            .ToDictionary(item => item.FeatureName, StringComparer.OrdinalIgnoreCase);
        foreach (var featureName in LicenseFeatureNames.All)
        {
            if (existingFeatures.TryGetValue(featureName, out var feature))
            {
                feature.SetEnabled(enabledFeatures.Contains(featureName));
                await _featureRepository.UpdateAsync(feature);
            }
            else
            {
                await _featureRepository.InsertAsync(new TenantLicenseFeature(
                    GuidGenerator.Create(),
                    license.Id,
                    featureName,
                    enabledFeatures.Contains(featureName)));
            }
        }

        await SetLimitAsync(license.Id, LicenseLimitNames.MaxUsers, input.MaxUsers);
        await SetLimitAsync(license.Id, LicenseLimitNames.MaxProducts, input.MaxProducts);
        await SetLimitAsync(license.Id, LicenseLimitNames.MaxBranches, input.MaxBranches);
        await AddAuditAsync(
            license,
            LicenseAuditActionNames.EntitlementsUpdated,
            license.Status,
            license.Status,
            $"Features: {string.Join(",", enabledFeatures.Order())}; " +
            $"Limits: users={FormatLimit(input.MaxUsers)}, products={FormatLimit(input.MaxProducts)}, branches={FormatLimit(input.MaxBranches)}");
        await RefreshAccessAsync(license.TenantId);
        return await GetAsync(id);
    }

    [Authorize(NoxStockPermissions.Licenses.Manage)]
    [UnitOfWork(isTransactional: true)]
    public async Task<TenantLicenseDto> ExtendAsync(Guid id, ExtendTenantLicenseDto input)
    {
        EnsureHost();
        var license = await _licenseRepository.GetAsync(id);
        var previousStatus = license.Status;
        var previousExpiry = license.ExpiresAtUtc;
        license.Extend(input.ExpiresAtUtc, input.GracePeriodEndsAtUtc, input.InternalNote);
        await _licenseRepository.UpdateAsync(license, autoSave: true);
        await AddAuditAsync(
            license,
            LicenseAuditActionNames.Extended,
            previousStatus,
            license.Status,
            $"{previousExpiry:O} -> {license.ExpiresAtUtc:O}");
        await RefreshAccessAsync(license.TenantId);
        return await GetAsync(id);
    }

    [Authorize(NoxStockPermissions.Licenses.Manage)]
    [UnitOfWork(isTransactional: true)]
    public async Task<TenantLicenseDto> SuspendAsync(Guid id, SuspendTenantLicenseDto input)
    {
        EnsureHost();
        var license = await _licenseRepository.GetAsync(id);
        var previousStatus = license.Status;
        license.Suspend(input.Reason);
        await _licenseRepository.UpdateAsync(license, autoSave: true);
        await AddAuditAsync(
            license,
            LicenseAuditActionNames.Suspended,
            previousStatus,
            license.Status,
            input.Reason);
        await RefreshAccessAsync(license.TenantId, LicenseStatus.Suspended);
        return await GetAsync(id);
    }

    [Authorize(NoxStockPermissions.Licenses.Manage)]
    [UnitOfWork(isTransactional: true)]
    public async Task<TenantLicenseDto> ReactivateAsync(Guid id)
    {
        EnsureHost();
        var license = await _licenseRepository.GetAsync(id);
        var previousStatus = license.Status;
        license.Reactivate();
        await _licenseRepository.UpdateAsync(license, autoSave: true);
        await AddAuditAsync(
            license,
            LicenseAuditActionNames.Reactivated,
            previousStatus,
            license.Status,
            null);
        await RefreshAccessAsync(license.TenantId);
        return await GetAsync(id);
    }

    [Authorize(NoxStockPermissions.Licenses.Manage)]
    [UnitOfWork(isTransactional: true)]
    public async Task<TenantLicenseDto> ChangeStatusAsync(Guid id, ChangeTenantLicenseStatusDto input)
    {
        EnsureHost();
        var license = await _licenseRepository.GetAsync(id);
        var previousStatus = license.Status;
        license.ChangeStatus(input.Status, input.Reason);
        await _licenseRepository.UpdateAsync(license, autoSave: true);
        await AddAuditAsync(
            license,
            input.Status == LicenseStatus.Cancelled
                ? LicenseAuditActionNames.Cancelled
                : LicenseAuditActionNames.StatusChanged,
            previousStatus,
            license.Status,
            input.Reason);
        await RefreshAccessAsync(
            license.TenantId,
            input.Status is LicenseStatus.PastDue
                or LicenseStatus.Suspended
                or LicenseStatus.Expired
                or LicenseStatus.Cancelled
                ? input.Status
                : null);
        return await GetAsync(id);
    }

    private async Task RefreshAccessAsync(
        Guid tenantId,
        LicenseStatus? restrictiveStatus = null)
    {
        if (restrictiveStatus.HasValue)
        {
            await _accessChecker.RestrictAsync(tenantId, restrictiveStatus.Value);
        }
        else
        {
            await _accessChecker.InvalidateAsync(tenantId);
        }

        _unitOfWorkManager.Current?.OnCompleted(
            () => _accessChecker.InvalidateAsync(tenantId));
    }

    private async Task<TenantLicenseDto> MapLicenseAsync(
        TenantLicense license,
        string tenantName,
        LicensePlan plan)
    {
        var features = await _featureRepository.GetListAsync(
            item => item.TenantLicenseId == license.Id);
        var limits = await _limitRepository.GetListAsync(
            item => item.TenantLicenseId == license.Id);

        return new TenantLicenseDto
        {
            Id = license.Id,
            TenantId = license.TenantId,
            TenantName = tenantName,
            LicensePlanId = license.LicensePlanId,
            PlanCode = plan.Code,
            PlanName = plan.Name,
            Status = license.Status,
            EffectiveStatus = license.GetEffectiveStatus(_clock.Now.ToUniversalTime()),
            StartsAtUtc = license.StartsAtUtc,
            ExpiresAtUtc = license.ExpiresAtUtc,
            GracePeriodEndsAtUtc = license.GracePeriodEndsAtUtc,
            LicenseReference = license.LicenseReference,
            IsAutoRenew = license.IsAutoRenew,
            InternalNote = license.InternalNote,
            SuspensionReason = license.SuspensionReason,
            CreationTime = license.CreationTime,
            CreatorId = license.CreatorId,
            LastModificationTime = license.LastModificationTime,
            LastModifierId = license.LastModifierId,
            Features = features.Select(item => new TenantLicenseFeatureDto
            {
                FeatureName = item.FeatureName,
                IsEnabled = item.IsEnabled
            }).ToList(),
            Limits = limits.Select(item => new TenantLicenseLimitDto
            {
                LimitName = item.LimitName,
                LimitValue = item.LimitValue
            }).ToList()
        };
    }

    private async Task ReplaceEntitlementsAsync(Guid tenantLicenseId, string planCode)
    {
        var existingFeatures = await _featureRepository.GetListAsync(
            item => item.TenantLicenseId == tenantLicenseId);
        var existingLimits = await _limitRepository.GetListAsync(
            item => item.TenantLicenseId == tenantLicenseId);

        await _featureRepository.DeleteManyAsync(existingFeatures);
        await _limitRepository.DeleteManyAsync(existingLimits);

        foreach (var featureName in LicenseFeatureNames.All)
        {
            await _featureRepository.InsertAsync(
                new TenantLicenseFeature(
                    GuidGenerator.Create(),
                    tenantLicenseId,
                    featureName,
                    isEnabled: true));
        }

        var isPilot = string.Equals(planCode, "PILOT", StringComparison.OrdinalIgnoreCase);
        await _limitRepository.InsertAsync(
            new TenantLicenseLimit(
                GuidGenerator.Create(),
                tenantLicenseId,
                LicenseLimitNames.MaxUsers,
                isPilot ? LicensePlanDefaults.PilotMaxUsers : null));
        await _limitRepository.InsertAsync(
            new TenantLicenseLimit(
                GuidGenerator.Create(),
                tenantLicenseId,
                LicenseLimitNames.MaxProducts,
                isPilot ? LicensePlanDefaults.PilotMaxProducts : null));
        await _limitRepository.InsertAsync(
            new TenantLicenseLimit(
                GuidGenerator.Create(),
                tenantLicenseId,
                LicenseLimitNames.MaxBranches,
                isPilot ? LicensePlanDefaults.PilotMaxBranches : null));
    }

    private async Task SetLimitAsync(Guid tenantLicenseId, string limitName, int? value)
    {
        var limit = await _limitRepository.FirstOrDefaultAsync(
            item => item.TenantLicenseId == tenantLicenseId && item.LimitName == limitName);
        if (limit is null)
        {
            await _limitRepository.InsertAsync(new TenantLicenseLimit(
                GuidGenerator.Create(),
                tenantLicenseId,
                limitName,
                value));
            return;
        }

        limit.SetValue(value);
        await _limitRepository.UpdateAsync(limit);
    }

    private static string FormatLimit(int? value)
    {
        return value?.ToString() ?? "unlimited";
    }

    private Task AddAuditAsync(
        TenantLicense license,
        string action,
        LicenseStatus? previousStatus,
        LicenseStatus? newStatus,
        string? details)
    {
        return _auditRepository.InsertAsync(
            new TenantLicenseAuditLog(
                GuidGenerator.Create(),
                license.Id,
                license.TenantId,
                action,
                previousStatus,
                newStatus,
                details),
            autoSave: true);
    }

    private void EnsureHost()
    {
        if (CurrentTenant.Id.HasValue)
        {
            throw new Volo.Abp.Authorization.AbpAuthorizationException(
                "Lisans yönetimi yalnız platform yöneticisine açıktır.");
        }
    }

    private static LicensePlanDto MapPlan(LicensePlan plan)
    {
        return new LicensePlanDto
        {
            Id = plan.Id,
            Code = plan.Code,
            Name = plan.Name,
            Description = plan.Description,
            IsActive = plan.IsActive,
            IsCustom = plan.IsCustom,
            CreationTime = plan.CreationTime,
            CreatorId = plan.CreatorId,
            LastModificationTime = plan.LastModificationTime,
            LastModifierId = plan.LastModifierId
        };
    }
}
