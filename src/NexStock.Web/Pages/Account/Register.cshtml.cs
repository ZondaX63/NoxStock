using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NexStock.Tenants;
using Volo.Abp.Account.Web.Pages.Account;
using Volo.Abp.Validation;
using Volo.Abp;

namespace NexStock.Web.Pages.Account;

public class RegisterModel : AccountPageModel
{
    [BindProperty]
    public PostInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string ReturnUrl { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string ReturnUrlHash { get; set; } = string.Empty;

    private readonly ITenantRegistrationAppService _tenantRegistrationAppService;
    private readonly bool _publicRegistrationEnabled;

    public RegisterModel(
        ITenantRegistrationAppService tenantRegistrationAppService,
        IConfiguration configuration)
    {
        _tenantRegistrationAppService = tenantRegistrationAppService;
        _publicRegistrationEnabled = configuration.GetValue<bool>("App:PublicRegistrationEnabled");
    }

    public virtual Task<IActionResult> OnGetAsync()
    {
        if (!_publicRegistrationEnabled)
        {
            return Task.FromResult<IActionResult>(NotFound());
        }

        Input = new PostInput();
        return Task.FromResult<IActionResult>(Page());
    }

    public virtual async Task<IActionResult> OnPostAsync()
    {
        if (!_publicRegistrationEnabled)
        {
            return NotFound();
        }

        try
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var dto = new RegisterTenantDto
            {
                TenantName = Input.TenantName,
                AdminEmailAddress = Input.EmailAddress,
                AdminPassword = Input.Password
            };

            await _tenantRegistrationAppService.RegisterTenantAsync(dto);

            return Redirect($"/t/{Uri.EscapeDataString(Input.TenantName)}/Account/Login");
        }
        catch (AbpValidationException ex)
        {
            foreach (var validationResult in ex.ValidationErrors)
            {
                ModelState.AddModelError(string.Empty, validationResult.ErrorMessage ?? "Geçersiz değer.");
            }
            return Page();
        }
        catch (UserFriendlyException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
        catch (Exception)
        {
            ModelState.AddModelError(string.Empty, "Bir hata oluştu. Lütfen tekrar deneyin.");
            return Page();
        }
    }

    public class PostInput
    {
        [Required(ErrorMessage = "Şirket Adı zorunludur.")]
        [StringLength(64)]
        [Display(Name = "Şirket Adı")]
        public string TenantName { get; set; } = string.Empty;

        [Required(ErrorMessage = "E-posta adresi zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        [StringLength(256)]
        [Display(Name = "E-posta Adresi")]
        public string EmailAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [StringLength(128)]
        [Display(Name = "Şifre")]
        public string Password { get; set; } = string.Empty;
    }
}
