using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NoxStock.MultiTenancy;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Validation;
using Volo.Abp.Auditing;
using NoxStock.Licensing;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using AbpIdentityUser = Volo.Abp.Identity.IdentityUser;

namespace NoxStock.Web.Pages.TenantManagement.Tenants;

[DisableAuditing]
public class CreateModalModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
{
    private readonly ITenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IDataSeeder _dataSeeder;
    private readonly ICurrentTenant _currentTenant;
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly IdentityUserManager _identityUserManager;
    private readonly ILogger<CreateModalModel> _logger;
    private readonly IHostLicenseAppService _licenseAppService;
    private readonly IConfiguration _configuration;

    public CreateModalModel(
        ITenantManager tenantManager,
        ITenantRepository tenantRepository,
        IDataSeeder dataSeeder,
        ICurrentTenant currentTenant,
        IIdentityUserRepository identityUserRepository,
        IdentityUserManager identityUserManager,
        ILogger<CreateModalModel> logger,
        IHostLicenseAppService licenseAppService,
        IConfiguration configuration)
    {
        _tenantManager = tenantManager;
        _tenantRepository = tenantRepository;
        _dataSeeder = dataSeeder;
        _currentTenant = currentTenant;
        _identityUserRepository = identityUserRepository;
        _identityUserManager = identityUserManager;
        _logger = logger;
        _licenseAppService = licenseAppService;
        _configuration = configuration;
    }

    [BindProperty]
    public CreateTenantInput Input { get; set; } = new();

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var existingTenant = await _tenantRepository.FindByNameAsync(Input.CompanyName.ToUpperInvariant());
            if (existingTenant is not null)
            {
                throw new UserFriendlyException("Bu şirket giriş kodu daha önce alınmış.");
            }

            var tenant = await _tenantManager.CreateAsync(Input.CompanyName);
            tenant.SetNoxStockActive(true);
            await _tenantRepository.InsertAsync(tenant, autoSave: true);

            using (_currentTenant.Change(tenant.Id, tenant.Name))
            {
                await _dataSeeder.SeedAsync(
                    new DataSeedContext(tenant.Id)
                        .WithProperty("AdminEmail", Input.AdminEmailAddress)
                        .WithProperty("AdminPassword", Input.AdminPassword));

                var administrator = await _identityUserRepository.FindByNormalizedUserNameAsync("ADMIN");
                if (administrator is null)
                {
                    throw new UserFriendlyException("Şirket yöneticisi oluşturulamadı. Lütfen işlemi tekrar deneyin.");
                }

                var renameResult = await _identityUserManager.SetUserNameAsync(administrator, Input.AdminUserName);
                if (!renameResult.Succeeded)
                {
                    throw new UserFriendlyException(string.Join(" ", renameResult.Errors));
                }
            }

            var pilotPlan = (await _licenseAppService.GetPlansAsync())
                .First(plan => plan.Code == "PILOT");
            var trialDays = Math.Max(
                1,
                _configuration.GetValue("Licensing:DefaultTrialDays", 30));
            var graceDays = Math.Max(
                0,
                _configuration.GetValue("Licensing:DefaultGracePeriodDays", 7));
            var startsAtUtc = DateTime.UtcNow;
            var expiresAtUtc = startsAtUtc.AddDays(trialDays);
            await _licenseAppService.AssignAsync(new AssignTenantLicenseDto
            {
                TenantId = tenant.Id,
                LicensePlanId = pilotPlan.Id,
                Status = LicenseStatus.Trial,
                StartsAtUtc = startsAtUtc,
                ExpiresAtUtc = expiresAtUtc,
                GracePeriodEndsAtUtc = graceDays > 0
                    ? expiresAtUtc.AddDays(graceDays)
                    : null,
                LicenseReference = $"PILOT-{tenant.Id:N}",
                InternalNote = "Şirket oluşturma akışında otomatik atandı."
            });

            return new NoContentResult();
        }
        catch (UserFriendlyException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
        catch (AbpValidationException ex)
        {
            foreach (var error in ex.ValidationErrors)
            {
                ModelState.AddModelError(string.Empty, error.ErrorMessage ?? "Geçersiz değer.");
            }

            return Page();
        }
        catch (System.Exception ex)
        {
            _logger.LogError(
                "Tenant creation failed. TraceId: {TraceId}; CompanyCode: {CompanyCode}; ExceptionType: {ExceptionType}",
                HttpContext.TraceIdentifier,
                Input.CompanyName,
                ex.GetType().Name);
            ModelState.AddModelError(string.Empty, "Şirket oluşturulurken beklenmeyen bir hata oluştu. İşlem kaydedilmedi.");
            return Page();
        }
    }

    public class CreateTenantInput
    {
        [Required(ErrorMessage = "Şirket adı zorunludur.")]
        [StringLength(64)]
        [RegularExpression("^[a-zA-Z0-9][a-zA-Z0-9-]*$", ErrorMessage = "Şirket adı harf veya rakamla başlamalı; yalnızca harf, rakam ve tire içerebilir.")]
        public string CompanyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Yönetici kullanıcı adı zorunludur.")]
        [StringLength(64)]
        [RegularExpression("^[a-zA-Z0-9._-]+$", ErrorMessage = "Kullanıcı adı yalnızca harf, rakam, nokta, alt çizgi ve tire içerebilir.")]
        public string AdminUserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Yönetici e-posta adresi zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
        [StringLength(256)]
        public string AdminEmailAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "Yönetici şifresi zorunludur.")]
        [StringLength(128, MinimumLength = 6, ErrorMessage = "Şifre en az 6 karakter olmalıdır.")]
        public string AdminPassword { get; set; } = string.Empty;
    }
}
