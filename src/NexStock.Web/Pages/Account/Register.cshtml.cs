using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NexStock.Tenants;
using Volo.Abp.Account.Web.Pages.Account;
using Volo.Abp.Validation;
using Volo.Abp;

namespace NexStock.Web.Pages.Account;

public class RegisterModel : AccountPageModel
{
    [BindProperty]
    public PostInput Input { get; set; }

    [BindProperty(SupportsGet = true)]
    public string ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public string ReturnUrlHash { get; set; }

    private readonly ITenantRegistrationAppService _tenantRegistrationAppService;

    public RegisterModel(ITenantRegistrationAppService tenantRegistrationAppService)
    {
        _tenantRegistrationAppService = tenantRegistrationAppService;
    }

    public virtual Task<IActionResult> OnGetAsync()
    {
        Input = new PostInput();
        return Task.FromResult<IActionResult>(Page());
    }

    public virtual async Task<IActionResult> OnPostAsync()
    {
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

            return RedirectToPage("Login", new
            {
                ReturnUrl = ReturnUrl,
                ReturnUrlHash = ReturnUrlHash,
                tenantName = Input.TenantName // Pass it to login page if we want
            });
        }
        catch (AbpValidationException ex)
        {
            foreach (var validationResult in ex.ValidationErrors)
            {
                ModelState.AddModelError(string.Empty, validationResult.ErrorMessage);
            }
            return Page();
        }
        catch (UserFriendlyException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
        catch (Exception ex)
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
        public string TenantName { get; set; }

        [Required(ErrorMessage = "E-posta adresi zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        [StringLength(256)]
        [Display(Name = "E-posta Adresi")]
        public string EmailAddress { get; set; }

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [StringLength(128)]
        [Display(Name = "Şifre")]
        public string Password { get; set; }
    }
}
