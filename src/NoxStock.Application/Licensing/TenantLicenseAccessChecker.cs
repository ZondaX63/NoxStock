using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using NoxStock.MultiTenancy;
using Volo.Abp;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace NoxStock.Licensing;

public class TenantLicenseAccessChecker : ITenantLicenseAccessChecker, ITransientDependency
{
    private readonly IRepository<TenantLicense, Guid> _licenseRepository;
    private readonly IRepository<LicensePlan, Guid> _planRepository;
    private readonly IRepository<TenantLicenseFeature, Guid> _featureRepository;
    private readonly IRepository<TenantLicenseLimit, Guid> _limitRepository;
    private readonly IDistributedCache<TenantLicenseAccessCacheItem, Guid> _cache;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IClock _clock;
    private readonly bool _enforcementEnabled;

    public TenantLicenseAccessChecker(
        IRepository<TenantLicense, Guid> licenseRepository,
        IRepository<LicensePlan, Guid> planRepository,
        IRepository<TenantLicenseFeature, Guid> featureRepository,
        IRepository<TenantLicenseLimit, Guid> limitRepository,
        IDistributedCache<TenantLicenseAccessCacheItem, Guid> cache,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IClock clock,
        IConfiguration configuration)
    {
        _licenseRepository = licenseRepository;
        _planRepository = planRepository;
        _featureRepository = featureRepository;
        _limitRepository = limitRepository;
        _cache = cache;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
        _clock = clock;
        _enforcementEnabled = configuration.GetValue("Licensing:EnforcementEnabled", true);
    }

    public Task<TenantLicenseAccessDto> GetCurrentAsync()
    {
        return GetAsync(_currentTenant.RequireTenantId());
    }

    public async Task<TenantLicenseAccessDto> GetAsync(Guid tenantId)
    {
        if (!_enforcementEnabled)
        {
            return CreateUnrestrictedAccess(tenantId);
        }

        return await _cache.GetOrAddAsync(
            tenantId,
            () => LoadAsync(tenantId),
            () => new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
            }) ?? CreateMissingLicenseAccess(tenantId);
    }

    public async Task EnsureCanWriteAsync()
    {
        var access = await GetCurrentAsync();
        EnsureCanWrite(access);
    }

    public async Task EnsureCanWriteAsync(Guid tenantId)
    {
        var access = await GetAsync(tenantId);
        EnsureCanWrite(access);
    }

    private static void EnsureCanWrite(TenantLicenseAccessDto access)
    {
        if (access.CanWrite)
        {
            return;
        }

        throw CreateAccessException(access.Status);
    }

    public async Task EnsureFeatureAsync(string featureName)
    {
        var access = await GetCurrentAsync();
        if (access.Features.TryGetValue(featureName, out var enabled) && enabled)
        {
            return;
        }

        throw new BusinessException(
                LicenseErrorCodes.FeatureNotIncluded,
                "Bu özellik mevcut lisans planına dahil değil.")
            .WithData("Feature", featureName);
    }

    public async Task EnsureLimitAsync(string limitName, long currentCount)
    {
        var access = await GetCurrentAsync();
        if (!access.Limits.TryGetValue(limitName, out var limit) ||
            !limit.HasValue ||
            currentCount < limit.Value)
        {
            return;
        }

        throw new BusinessException(
                LicenseErrorCodes.LimitReached,
                "Lisans planı limiti aşıldı.")
            .WithData("Limit", limitName)
            .WithData("LimitValue", limit.Value)
            .WithData("CurrentValue", currentCount);
    }

    public Task InvalidateAsync(Guid tenantId)
    {
        return _cache.RemoveAsync(tenantId);
    }

    public Task RestrictAsync(Guid tenantId, LicenseStatus status)
    {
        return _cache.SetAsync(
            tenantId,
            new TenantLicenseAccessCacheItem
            {
                TenantId = tenantId,
                Status = status,
                CanRead = true,
                CanWrite = false
            },
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
            });
    }

    private async Task<TenantLicenseAccessCacheItem> LoadAsync(Guid tenantId)
    {
        using (_currentTenant.Change(null))
        {
            using var unitOfWork = _unitOfWorkManager.Begin(
                requiresNew: true,
                isTransactional: false);

            var license = await _licenseRepository.FirstOrDefaultAsync(
                item => item.TenantId == tenantId && item.IsCurrent);
            if (license is null)
            {
                await unitOfWork.CompleteAsync();
                return CreateMissingLicenseAccess(tenantId);
            }

            var plan = await _planRepository.GetAsync(license.LicensePlanId);
            var features = await _featureRepository.GetListAsync(
                item => item.TenantLicenseId == license.Id);
            var limits = await _limitRepository.GetListAsync(
                item => item.TenantLicenseId == license.Id);
            var effectiveStatus = license.GetEffectiveStatus(_clock.Now.ToUniversalTime());

            var result = new TenantLicenseAccessCacheItem
            {
                TenantId = tenantId,
                TenantLicenseId = license.Id,
                PlanCode = plan.Code,
                Status = effectiveStatus,
                ExpiresAtUtc = license.ExpiresAtUtc,
                GracePeriodEndsAtUtc = license.GracePeriodEndsAtUtc,
                CanRead = true,
                CanWrite = effectiveStatus is LicenseStatus.Trial
                    or LicenseStatus.Active
                    or LicenseStatus.GracePeriod,
                Features = features.ToDictionary(
                    item => item.FeatureName,
                    item => item.IsEnabled,
                    StringComparer.OrdinalIgnoreCase),
                Limits = limits.ToDictionary(
                    item => item.LimitName,
                    item => item.LimitValue,
                    StringComparer.OrdinalIgnoreCase)
            };

            await unitOfWork.CompleteAsync();
            return result;
        }
    }

    private static TenantLicenseAccessCacheItem CreateMissingLicenseAccess(Guid tenantId)
    {
        return new TenantLicenseAccessCacheItem
        {
            TenantId = tenantId,
            CanRead = true,
            CanWrite = false
        };
    }

    private static TenantLicenseAccessDto CreateUnrestrictedAccess(Guid tenantId)
    {
        return new TenantLicenseAccessDto
        {
            TenantId = tenantId,
            PlanCode = "TEST",
            Status = LicenseStatus.Active,
            CanRead = true,
            CanWrite = true,
            Features = LicenseFeatureNames.All.ToDictionary(
                feature => feature,
                _ => true,
                StringComparer.OrdinalIgnoreCase)
        };
    }

    private static BusinessException CreateAccessException(LicenseStatus? status)
    {
        return status switch
        {
            LicenseStatus.PastDue => new BusinessException(
                LicenseErrorCodes.PastDueReadOnly,
                "Ödeme gecikmesi nedeniyle çalışma alanı yalnız görüntüleme modundadır."),
            LicenseStatus.Suspended => new BusinessException(
                LicenseErrorCodes.Suspended,
                "Şirket lisansı askıya alınmıştır."),
            LicenseStatus.Expired => new BusinessException(
                LicenseErrorCodes.Expired,
                "Şirket lisansının süresi dolmuştur."),
            LicenseStatus.Cancelled => new BusinessException(
                LicenseErrorCodes.Cancelled,
                "Şirket lisansı iptal edilmiştir."),
            _ => new BusinessException(
                LicenseErrorCodes.Required,
                "Şirket için geçerli bir lisans bulunamadı.")
        };
    }
}
