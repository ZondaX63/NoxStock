using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NoxStock.MultiTenancy;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Auditing;
using Volo.Abp.TenantManagement;
using AbpIdentityUser = Volo.Abp.Identity.IdentityUser;

namespace NoxStock.Web.Pages.Account;

[DisableAuditing]
public class LoginModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
{
    private readonly SignInManager<AbpIdentityUser> _signInManager;
    private readonly IRepository<AbpIdentityUser, Guid> _identityUserRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<LoginModel> _logger;
    private readonly IOptionsMonitor<CookieAuthenticationOptions> _cookieOptions;

    public LoginModel(
        SignInManager<AbpIdentityUser> signInManager,
        IRepository<AbpIdentityUser, Guid> identityUserRepository,
        ITenantRepository tenantRepository,
        IDataFilter dataFilter,
        ICurrentTenant currentTenant,
        ILogger<LoginModel> logger,
        IOptionsMonitor<CookieAuthenticationOptions> cookieOptions)
    {
        _signInManager = signInManager;
        _identityUserRepository = identityUserRepository;
        _tenantRepository = tenantRepository;
        _dataFilter = dataFilter;
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
        // A login attempt must never keep a prior tenant session alive. This also
        // prevents a failed/invalid host login from leaving the browser in the
        // previously selected company workspace.
        await _signInManager.SignOutAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = default(AbpIdentityUser);
        Tenant? resolvedTenant = null;

        // The public login page is exclusively for company users. Platform
        // administrators have a separate /admin/login entry point.
        var matchingUsers = await FindMatchingUsersAsync(LoginInput.UserNameOrEmailAddress);
        if (matchingUsers.Any(candidate => !candidate.TenantId.HasValue))
        {
            _logger.LogWarning(
                "A platform administrator identifier was used on the public login page: {LoginIdentifier}.",
                LoginInput.UserNameOrEmailAddress);
            ModelState.AddModelError(
                string.Empty,
                "Platform yöneticisi hesabı bu ekrandan giriş yapamaz.");
            return Page();
        }

        var tenantUsers = matchingUsers
            .Where(candidate => candidate.TenantId.HasValue)
            .ToList();
        if (tenantUsers.Count > 1)
        {
            _logger.LogWarning(
                "Public login requires a tenant choice for {LoginIdentifier}; {TenantUserCount} tenant users matched.",
                LoginInput.UserNameOrEmailAddress,
                tenantUsers.Count);
            ModelState.AddModelError(
                string.Empty,
                "Bu kullanıcı adı birden fazla şirkette bulunuyor. Şirket yöneticinizle iletişime geçin.");
            return Page();
        }

        if (tenantUsers.Count == 1)
        {
            user = tenantUsers[0];
            resolvedTenant = await _tenantRepository.FindAsync(user.TenantId!.Value);
        }

        if (user is null)
        {
            _logger.LogWarning(
                "Login failed because no matching user was found for {LoginIdentifier} in tenant {TenantId}",
                LoginInput.UserNameOrEmailAddress,
                _currentTenant.Id);
            ModelState.AddModelError(string.Empty, "Kullanıcı adı veya şifre hatalı.");
            return Page();
        }

        // Password/lockout updates for a user found from the public login page must
        // still execute within that user's tenant scope.
        using var tenantScope = resolvedTenant is null
            ? null
            : _currentTenant.Change(resolvedTenant.Id, resolvedTenant.Name);

        var result = await _signInManager.CheckPasswordSignInAsync(user, LoginInput.Password, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var tenantClaims = user.TenantId.HasValue
                ? new[] { new Claim(AbpClaimTypes.TenantId, user.TenantId.Value.ToString()) }
                : Array.Empty<Claim>();

            await _signInManager.SignInWithClaimsAsync(user, LoginInput.RememberMe, tenantClaims);
            return LocalRedirect(GetSafeReturnUrl(resolvedTenant));
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
            : "Kullanıcı adı veya şifre hatalı.";

        ModelState.AddModelError(string.Empty, error);
        return Page();
    }

    private async Task<List<AbpIdentityUser>> FindMatchingUsersAsync(string loginIdentifier)
    {
        var normalizedIdentifier = loginIdentifier.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedIdentifier))
        {
            return [];
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var candidates = await _identityUserRepository.GetListAsync(user =>
                user.NormalizedUserName == normalizedIdentifier ||
                user.NormalizedEmail == normalizedIdentifier);

            return candidates
                .GroupBy(user => user.Id)
                .Select(group => group.First())
                .ToList();
        }
    }

    private string GetSafeReturnUrl(Tenant? resolvedTenant)
    {
        if (resolvedTenant is not null)
        {
            // A host-login tenant account must always enter its own workspace,
            // never a host-local return URL such as /admin.
            return $"/t/{Uri.EscapeDataString(resolvedTenant.Name)}/Dashboard";
        }

        var fallback = "/";
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
