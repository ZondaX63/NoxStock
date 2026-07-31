using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using NoxStock.Licensing;
using NoxStock.Localization;
using NoxStock.Permissions;

namespace NoxStock.Web.Pages.LicenseManagement;

[Authorize(NoxStockPermissions.Licenses.Default)]
public class IndexModel : PageModel
{
    private readonly IHostLicenseAppService _licenseAppService;
    private readonly IStringLocalizer<NoxStockResource> _localizer;

    public IndexModel(
        IHostLicenseAppService licenseAppService,
        IStringLocalizer<NoxStockResource> localizer)
    {
        _licenseAppService = licenseAppService;
        _localizer = localizer;
    }

    public List<HostTenantLicenseRowDto> Rows { get; private set; } = [];
    public List<LicensePlanDto> Plans { get; private set; } = [];
    public List<TenantLicenseAuditLogDto> AuditLogs { get; private set; } = [];
    public Guid? SelectedLicenseId { get; private set; }
    public TenantLicenseDto? SelectedLicense { get; private set; }

    public async Task OnGetAsync(Guid? licenseId)
    {
        await LoadAsync(licenseId);
    }

    public async Task<IActionResult> OnPostAssignAsync(
        Guid tenantId,
        Guid planId,
        LicenseStatus status = LicenseStatus.Trial,
        int durationDays = 30,
        int graceDays = 7,
        string? licenseReference = null,
        bool isAutoRenew = false)
    {
        var validDurationDays = Math.Clamp(durationDays, 1, 3650);
        var validGraceDays = Math.Clamp(graceDays, 0, 365);
        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddDays(validDurationDays);
        await _licenseAppService.AssignAsync(new AssignTenantLicenseDto
        {
            TenantId = tenantId,
            LicensePlanId = planId,
            Status = status is LicenseStatus.Active ? LicenseStatus.Active : LicenseStatus.Trial,
            StartsAtUtc = now,
            ExpiresAtUtc = expiresAtUtc,
            GracePeriodEndsAtUtc = validGraceDays > 0
                ? expiresAtUtc.AddDays(validGraceDays)
                : null,
            LicenseReference = string.IsNullOrWhiteSpace(licenseReference)
                ? $"LIC-{tenantId:N}-{Guid.NewGuid():N}"
                : licenseReference.Trim(),
            IsAutoRenew = isAutoRenew,
            InternalNote = "Host yönetim ekranından oluşturuldu."
        });
        TempData["LicenseMessage"] = _localizer["License:Message:Assigned"].Value;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostExtendAsync(Guid licenseId, int days = 30)
    {
        var license = await _licenseAppService.GetAsync(licenseId);
        var extensionDays = Math.Clamp(days, 1, 3650);
        var baseDate = license.ExpiresAtUtc > DateTime.UtcNow
            ? license.ExpiresAtUtc
            : DateTime.UtcNow;
        var newExpiry = baseDate.AddDays(extensionDays);
        await _licenseAppService.ExtendAsync(licenseId, new ExtendTenantLicenseDto
        {
            ExpiresAtUtc = newExpiry,
            GracePeriodEndsAtUtc = newExpiry.AddDays(7),
            InternalNote = $"{extensionDays} gün uzatıldı."
        });
        TempData["LicenseMessage"] = _localizer[
            "License:Message:Extended",
            extensionDays].Value;
        return RedirectToPage(new { licenseId });
    }

    public async Task<IActionResult> OnPostSuspendAsync(Guid licenseId, string reason)
    {
        await _licenseAppService.SuspendAsync(licenseId, new SuspendTenantLicenseDto
        {
            Reason = string.IsNullOrWhiteSpace(reason)
                ? "Platform yöneticisi tarafından askıya alındı."
                : reason.Trim()
        });
        TempData["LicenseMessage"] = _localizer["License:Message:Suspended"].Value;
        return RedirectToPage(new { licenseId });
    }

    public async Task<IActionResult> OnPostReactivateAsync(Guid licenseId)
    {
        await _licenseAppService.ReactivateAsync(licenseId);
        TempData["LicenseMessage"] = _localizer["License:Message:Reactivated"].Value;
        return RedirectToPage(new { licenseId });
    }

    public async Task<IActionResult> OnPostSetPastDueAsync(Guid licenseId)
    {
        await _licenseAppService.ChangeStatusAsync(licenseId, new ChangeTenantLicenseStatusDto
        {
            Status = LicenseStatus.PastDue,
            Reason = "Ödeme gecikmesi nedeniyle yalnız görüntüleme moduna alındı."
        });
        TempData["LicenseMessage"] = _localizer["License:Message:PastDue"].Value;
        return RedirectToPage(new { licenseId });
    }

    public async Task<IActionResult> OnPostExpireAsync(Guid licenseId)
    {
        await _licenseAppService.ChangeStatusAsync(licenseId, new ChangeTenantLicenseStatusDto
        {
            Status = LicenseStatus.Expired,
            Reason = "Platform yöneticisi tarafından süresi dolmuş olarak işaretlendi."
        });
        TempData["LicenseMessage"] = _localizer["License:Message:Expired"].Value;
        return RedirectToPage(new { licenseId });
    }

    public async Task<IActionResult> OnPostChangePlanAsync(
        Guid licenseId,
        Guid planId)
    {
        await _licenseAppService.ChangePlanAsync(licenseId, new ChangeTenantLicensePlanDto
        {
            LicensePlanId = planId,
            InternalNote = "Host yönetim ekranından plan değiştirildi."
        });
        TempData["LicenseMessage"] = _localizer["License:Message:PlanChanged"].Value;
        return RedirectToPage(new { licenseId });
    }

    public async Task<IActionResult> OnPostUpdateEntitlementsAsync(
        Guid licenseId,
        bool pos,
        bool multiCurrency,
        bool dynamicPricing,
        bool advancedReports,
        bool export,
        int? maxUsers,
        int? maxProducts,
        int? maxBranches)
    {
        var enabledFeatures = new List<string>();
        if (pos) enabledFeatures.Add(LicenseFeatureNames.Pos);
        if (multiCurrency) enabledFeatures.Add(LicenseFeatureNames.MultiCurrency);
        if (dynamicPricing) enabledFeatures.Add(LicenseFeatureNames.DynamicPricing);
        if (advancedReports) enabledFeatures.Add(LicenseFeatureNames.AdvancedReports);
        if (export) enabledFeatures.Add(LicenseFeatureNames.Export);

        await _licenseAppService.UpdateEntitlementsAsync(
            licenseId,
            new UpdateTenantLicenseEntitlementsDto
            {
                EnabledFeatures = enabledFeatures,
                MaxUsers = maxUsers,
                MaxProducts = maxProducts,
                MaxBranches = maxBranches
            });
        TempData["LicenseMessage"] = _localizer["License:Message:EntitlementsUpdated"].Value;
        return RedirectToPage(new { licenseId });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid licenseId)
    {
        await _licenseAppService.DeleteAsync(licenseId);
        TempData["LicenseMessage"] = _localizer["License:Message:Deleted"].Value;
        return RedirectToPage();
    }

    private async Task LoadAsync(Guid? licenseId)
    {
        Rows = await _licenseAppService.GetOverviewAsync();
        Plans = (await _licenseAppService.GetPlansAsync())
            .Where(plan => plan.IsActive)
            .ToList();
        SelectedLicenseId = licenseId;
        if (licenseId.HasValue)
        {
            SelectedLicense = await _licenseAppService.GetAsync(licenseId.Value);
            AuditLogs = await _licenseAppService.GetAuditLogAsync(licenseId.Value);
        }
    }
}
