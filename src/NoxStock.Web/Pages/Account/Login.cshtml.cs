using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NoxStock.MultiTenancy;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using AbpIdentityUser = Volo.Abp.Identity.IdentityUser;

namespace NoxStock.Web.Pages.Account;

public class LoginModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
{
    private readonly SignInManager<AbpIdentityUser> _signInManager;
    private readonly UserManager<AbpIdentityUser> _userManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<LoginModel> _logger;
    private readonly IOptionsMonitor<CookieAuthenticationOptions> _cookieOptions;

    public LoginModel(
        SignInManager<AbpIdentityUser> signInManager,
        UserManager<AbpIdentityUser> userManager,
        ICurrentTenant currentTenant,
        ILogger<LoginModel> logger,
        IOptionsMonitor<CookieAuthenticationOptions> cookieOptions)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _currentTenant = currentTenant;
        _logger = logger;
        _cookieOptions = cookieOptions;
    }

    [BindProperty]
    public LoginInputModel LoginInput { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrlHash { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        DeleteLegacyTenantCookie();

        if (_signInManager.IsSignedIn(User))
        {
            await _signInManager.SignOutAsync();
            return LocalRedirect($"{Request.PathBase}{Request.Path}{Request.QueryString}");
        }

        return Page();
    }

    private void DeleteLegacyTenantCookie()
    {
        if (!Request.PathBase.HasValue)
        {
            return;
        }

        var cookieName = _cookieOptions
            .Get(IdentityConstants.ApplicationScheme)
            .Cookie.Name;

        if (!string.IsNullOrWhiteSpace(cookieName))
        {
            Response.Cookies.Delete(cookieName, new CookieOptions { Path = Request.PathBase.Value });
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _userManager.FindByNameAsync(LoginInput.UserNameOrEmailAddress)
                   ?? await _userManager.FindByEmailAsync(LoginInput.UserNameOrEmailAddress);

        if (user is null)
        {
            _logger.LogWarning(
                "Login failed because no matching user was found for {LoginIdentifier} in tenant {TenantId}",
                LoginInput.UserNameOrEmailAddress,
                _currentTenant.Id);
            ModelState.AddModelError(string.Empty, "Kullanıcı adı veya şifre hatalı.");
            return Page();
        }

        var result = await _signInManager.CheckPasswordSignInAsync(
            user,
            LoginInput.Password,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var tenantClaims = user.TenantId.HasValue
                ? new[] { new Claim(AbpClaimTypes.TenantId, user.TenantId.Value.ToString()) }
                : Array.Empty<Claim>();

            await _signInManager.SignInWithClaimsAsync(user, LoginInput.RememberMe, tenantClaims);
            return LocalRedirect(GetSafeReturnUrl());
        }

        _logger.LogWarning(
            "Login failed for user {UserId} in tenant {TenantId}. LockedOut: {IsLockedOut}; NotAllowed: {IsNotAllowed}",
            user.Id,
            _currentTenant.Id,
            result.IsLockedOut,
            result.IsNotAllowed);

        var error = result.IsLockedOut
            ? "Hesabınız geçici olarak kilitlendi. Lütfen daha sonra tekrar deneyin veya bir yöneticiyle iletişime geçin."
            : result.IsNotAllowed
                ? "Hesap henüz giriş için uygun değil. Yönetici hesabı etkinleştirmesini kontrol edin."
            : _currentTenant.Id.HasValue
                ? "Kullanıcı adı veya şifre hatalı. Bu şirket için oluşturulan yönetici hesabını kullanın."
                : "Kullanıcı adı veya şifre hatalı. Şirket hesabına giriş için size ait şirket bağlantısını kullanın.";

        ModelState.AddModelError(string.Empty, error);
        return Page();
    }

    private string GetSafeReturnUrl()
    {
        var fallback = _currentTenant.Id.HasValue ? "/Dashboard" : "/TenantManagement/Tenants";
        var returnUrl = ReturnUrl?.Trim();

        if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
        {
            return Request.PathBase.Add(fallback).Value!;
        }

        if (Request.PathBase.HasValue && !returnUrl.StartsWith(Request.PathBase.Value!, System.StringComparison.OrdinalIgnoreCase))
        {
            return Request.PathBase.Add(returnUrl).Value!;
        }

        return returnUrl;
    }

    public class LoginInputModel
    {
        [Required(ErrorMessage = "Kullanıcı adı veya e-posta adresi zorunludur.")]
        [StringLength(256)]
        public string UserNameOrEmailAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [StringLength(128)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
