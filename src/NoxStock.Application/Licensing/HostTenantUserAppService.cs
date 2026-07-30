using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.Permissions;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Timing;
using AbpIdentityUser = Volo.Abp.Identity.IdentityUser;

namespace NoxStock.Licensing;

[Authorize(NoxStockPermissions.Licenses.Manage)]
public class HostTenantUserAppService : NoxStockAppService, IHostTenantUserAppService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IIdentityUserRepository _userRepository;
    private readonly IRepository<TenantLicense, Guid> _licenseRepository;
    private readonly IRepository<TenantLicenseLimit, Guid> _limitRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;

    public HostTenantUserAppService(
        ITenantRepository tenantRepository,
        IIdentityUserRepository userRepository,
        IRepository<TenantLicense, Guid> licenseRepository,
        IRepository<TenantLicenseLimit, Guid> limitRepository,
        IdentityUserManager userManager,
        ICurrentTenant currentTenant,
        IClock clock)
    {
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _licenseRepository = licenseRepository;
        _limitRepository = limitRepository;
        _userManager = userManager;
        _currentTenant = currentTenant;
        _clock = clock;
    }

    public async Task<HostTenantUserDto> CreateAsync(CreateHostTenantUserDto input)
    {
        EnsureHost();
        var tenant = await _tenantRepository.GetAsync(input.TenantId);
        var userLimit = await EnsureLicenseAllowsUserCreationAsync(tenant.Id);

        using (_currentTenant.Change(tenant.Id, tenant.Name))
        {
            var currentUsers = await _userRepository.GetCountAsync();
            if (userLimit.HasValue && currentUsers >= userLimit.Value)
            {
                throw new BusinessException(
                        LicenseErrorCodes.LimitReached,
                        "Lisans planı kullanıcı limiti aşıldı.")
                    .WithData("Limit", LicenseLimitNames.MaxUsers)
                    .WithData("LimitValue", userLimit.Value)
                    .WithData("CurrentValue", currentUsers);
            }

            var user = new AbpIdentityUser(
                GuidGenerator.Create(),
                input.UserName.Trim(),
                input.Email.Trim(),
                tenant.Id);
            var result = await _userManager.CreateAsync(user, input.Password);
            if (!result.Succeeded)
            {
                throw new UserFriendlyException(
                    string.Join(" ", result.Errors.Select(error => error.Description)));
            }

            return new HostTenantUserDto
            {
                Id = user.Id,
                TenantId = tenant.Id,
                UserName = user.UserName ?? input.UserName,
                Email = user.Email ?? input.Email
            };
        }
    }

    private void EnsureHost()
    {
        if (_currentTenant.Id.HasValue)
        {
            throw new Volo.Abp.Authorization.AbpAuthorizationException(
                "Kullanici yonetimi yalniz platform yoneticisine aciktir.");
        }
    }

    private async Task<int?> EnsureLicenseAllowsUserCreationAsync(Guid tenantId)
    {
        var license = await _licenseRepository.FirstOrDefaultAsync(
            item => item.TenantId == tenantId && item.IsCurrent);
        if (license is null)
        {
            throw new BusinessException(
                LicenseErrorCodes.Required,
                "Şirket için geçerli bir lisans bulunamadı.");
        }

        var status = license.GetEffectiveStatus(_clock.Now.ToUniversalTime());
        if (status is not (LicenseStatus.Trial or LicenseStatus.Active or LicenseStatus.GracePeriod))
        {
            throw CreateAccessException(status);
        }

        var limit = await _limitRepository.FirstOrDefaultAsync(
            item => item.TenantLicenseId == license.Id &&
                    item.LimitName == LicenseLimitNames.MaxUsers);
        return limit?.LimitValue;
    }

    private static BusinessException CreateAccessException(LicenseStatus status)
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
