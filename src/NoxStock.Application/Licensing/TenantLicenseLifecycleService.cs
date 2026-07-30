using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace NoxStock.Licensing;

public class TenantLicenseLifecycleService : ITenantLicenseLifecycleService, ITransientDependency
{
    private readonly IRepository<TenantLicense, Guid> _licenseRepository;
    private readonly IRepository<TenantLicenseAuditLog, Guid> _auditRepository;
    private readonly ITenantLicenseAccessChecker _accessChecker;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IClock _clock;

    public TenantLicenseLifecycleService(
        IRepository<TenantLicense, Guid> licenseRepository,
        IRepository<TenantLicenseAuditLog, Guid> auditRepository,
        ITenantLicenseAccessChecker accessChecker,
        ICurrentTenant currentTenant,
        IGuidGenerator guidGenerator,
        IClock clock)
    {
        _licenseRepository = licenseRepository;
        _auditRepository = auditRepository;
        _accessChecker = accessChecker;
        _currentTenant = currentTenant;
        _guidGenerator = guidGenerator;
        _clock = clock;
    }

    [UnitOfWork(isTransactional: true)]
    public async Task ProcessAsync()
    {
        using (_currentTenant.Change(null))
        {
            var now = _clock.Now.ToUniversalTime();
            var licenses = await _licenseRepository.GetListAsync(item => item.IsCurrent);

            foreach (var license in licenses)
            {
                var effectiveStatus = license.GetEffectiveStatus(now);
                if (effectiveStatus != license.Status &&
                    license.Status is LicenseStatus.Trial
                        or LicenseStatus.Active
                        or LicenseStatus.GracePeriod)
                {
                    var previousStatus = license.Status;
                    license.ChangeStatus(effectiveStatus);
                    await _licenseRepository.UpdateAsync(license);
                    await AddAuditAsync(
                        license,
                        LicenseAuditActionNames.StatusChanged,
                        previousStatus,
                        effectiveStatus,
                        "Lisans durumu sunucu UTC saatine göre otomatik güncellendi.");
                    await _accessChecker.InvalidateAsync(license.TenantId);
                }

                await CreateExpiryNoticeIfDueAsync(license, now, 14, LicenseAuditActionNames.ExpiryNotice14Days);
                await CreateExpiryNoticeIfDueAsync(license, now, 7, LicenseAuditActionNames.ExpiryNotice7Days);
                await CreateExpiryNoticeIfDueAsync(license, now, 1, LicenseAuditActionNames.ExpiryNotice1Day);
            }
        }
    }

    private async Task CreateExpiryNoticeIfDueAsync(
        TenantLicense license,
        DateTime utcNow,
        int days,
        string action)
    {
        var remaining = license.ExpiresAtUtc - utcNow;
        if (remaining <= TimeSpan.Zero || remaining > TimeSpan.FromDays(days))
        {
            return;
        }

        var exists = await _auditRepository.AnyAsync(
            item => item.TenantLicenseId == license.Id && item.Action == action);
        if (exists)
        {
            return;
        }

        await AddAuditAsync(
            license,
            action,
            license.Status,
            license.Status,
            $"Lisans bitişine {days} gün veya daha az kaldı.");
    }

    private Task AddAuditAsync(
        TenantLicense license,
        string action,
        LicenseStatus? previousStatus,
        LicenseStatus? newStatus,
        string details)
    {
        return _auditRepository.InsertAsync(
            new TenantLicenseAuditLog(
                _guidGenerator.Create(),
                license.Id,
                license.TenantId,
                action,
                previousStatus,
                newStatus,
                details));
    }
}
