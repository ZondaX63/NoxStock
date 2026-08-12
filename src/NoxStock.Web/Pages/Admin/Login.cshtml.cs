using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NoxStock.MultiTenancy;
using Volo.Abp.Auditing;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using AbpIdentityUser = Volo.Abp.Identity.IdentityUser;

namespace NoxStock.Web.Pages.Admin;

[DisableAuditing]
public class LoginModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
{
    private readonly SignInManager<AbpIdentityUser> _signInManager;
    private readonly UserManager<AbpIdentityUser> _userManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        SignInManager<AbpIdentityUser> signInManager,
        UserManager<AbpIdentityUser> userManager,
        ICurrentTenant currentTenant,
        ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [BindProperty]
    public LoginInputModel LoginInput { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_signInManager.IsSignedIn(User))
        {
            await _signInManager.SignOutAsync();
            return LocalRedirect("/admin/login");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Never retain a prior company session when an administrator login is
        // attempted, including when credentials are invalid.
        await _signInManager.SignOutAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        using (_currentTenant.Change(null))
        {
            var user = await _userManager.FindByNameAsync(LoginInput.UserNameOrEmailAddress)
                       ?? await _userManager.FindByEmailAsync(LoginInput.UserNameOrEmailAddress);

            if (user is null || user.TenantId.HasValue)
            {
                _logger.LogWarning("Platform login failed for {LoginIdentifier}.", LoginInput.UserNameOrEmailAddress);
                ModelState.AddModelError(string.Empty, "Platform yöneticisi kullanıcı adı veya şifre hatalı.");
                return Page();
            }

            var result = await _signInManager.CheckPasswordSignInAsync(
                user,
                LoginInput.Password,
                lockoutOnFailure: true);

            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Platform login failed for user {UserId}. LockedOut: {IsLockedOut}; NotAllowed: {IsNotAllowed}",
                    user.Id,
                    result.IsLockedOut,
                    result.IsNotAllowed);
                ModelState.AddModelError(
                    string.Empty,
                    result.IsLockedOut
                        ? "Hesabınız geçici olarak kilitlendi. Lütfen daha sonra tekrar deneyin."
                        : "Platform yöneticisi kullanıcı adı veya şifre hatalı.");
                return Page();
            }

            await _signInManager.SignInWithClaimsAsync(
                user,
                LoginInput.RememberMe,
                Array.Empty<Claim>());
        }

        return LocalRedirect(GetSafeReturnUrl());
    }

    private string GetSafeReturnUrl()
    {
        var returnUrl = ReturnUrl?.Trim();
        if (string.IsNullOrWhiteSpace(returnUrl) ||
            !Url.IsLocalUrl(returnUrl) ||
            !returnUrl.StartsWith("/admin", StringComparison.OrdinalIgnoreCase))
        {
            return "/admin";
        }

        return returnUrl;
    }

    public class LoginInputModel
    {
        [Required(ErrorMessage = "Yönetici kullanıcı adı veya e-posta adresi zorunludur.")]
        [StringLength(256)]
        public string UserNameOrEmailAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [StringLength(128)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
