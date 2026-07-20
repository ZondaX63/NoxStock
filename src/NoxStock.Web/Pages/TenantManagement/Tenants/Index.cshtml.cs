using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NoxStock.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace NoxStock.Web.Pages.TenantManagement.Tenants;

[Authorize("AbpTenantManagement.Tenants")]
public class IndexModel : PageModel
{
    private readonly ITenantRepository _tenantRepository;

    public IndexModel(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    public List<Tenant> Tenants { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Tenants = (await _tenantRepository.GetListAsync())
            .OrderByDescending(tenant => tenant.CreationTime)
            .ToList();
    }

    public async Task<IActionResult> OnPostSetActiveAsync(Guid tenantId, bool isActive)
    {
        var tenant = await _tenantRepository.GetAsync(tenantId);
        tenant.SetNoxStockActive(isActive);
        await _tenantRepository.UpdateAsync(tenant, autoSave: true);

        TempData["TenantStatusMessage"] = isActive
            ? $"{tenant.Name} çalışma alanı etkinleştirildi."
            : $"{tenant.Name} çalışma alanı pasife alındı.";

        return RedirectToPage();
    }

    /// <summary>
    /// Host admin session'ını temizleyip tenant login sayfasına yönlendirir.
    /// Aynı tarayıcıda host cookie'leriyle tenant login'e gidilince 403 oluşuyor;
    /// bu handler önce sign-out yaparak o çakışmayı ortadan kaldırır.
    /// </summary>
    public async Task<IActionResult> OnGetSwitchToTenantAsync(string tenantName)
    {
        if (string.IsNullOrWhiteSpace(tenantName))
            return BadRequest();

        // Host session'ını kapat
        await HttpContext.SignOutAsync();

        // Tüm auth cookie'leri temizle
        foreach (var cookieName in Request.Cookies.Keys)
        {
            Response.Cookies.Delete(cookieName);
        }

        // Tenant login sayfasına yönlendir
        return Redirect($"/t/{tenantName}/Account/Login");
    }
}
