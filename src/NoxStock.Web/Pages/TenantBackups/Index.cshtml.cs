using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoxStock.Permissions;
using NoxStock.Settings;
using NoxStock.TenantBackups;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.MultiTenancy;

namespace NoxStock.Web.Pages.TenantBackups;

[Authorize(NoxStockPermissions.TenantBackups.Default)]
[RequestSizeLimit(512L * 1024L * 1024L)]
public class IndexModel : NoxStockPageModel
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantRestoreAppService _restore;
    private readonly ITenantOperationalDataResetAppService _resetService;
    private readonly IPermissionChecker _permissionChecker;

    public bool IsHost { get; private set; }
    [BindProperty] public Guid SourceBackupId { get; set; }
    [BindProperty] public string RestorePassword { get; set; } = string.Empty;
    [BindProperty] public string TargetTenantName { get; set; } = string.Empty;
    [BindProperty] public string TargetTenantCode { get; set; } = string.Empty;
    [BindProperty] public string AdminEmailAddress { get; set; } = string.Empty;
    [BindProperty] public string AdminPassword { get; set; } = string.Empty;
    [BindProperty] public string OperationKey { get; set; } = Guid.NewGuid().ToString("N");
    [BindProperty] public TenantOperationalDataResetInputDto ResetInput { get; set; } = new();
    public bool CanResetOperationalData { get; private set; }
    public TenantBackupRestorePreviewDto? Preview { get; private set; }
    public TenantCloneOperationDto? CloneOperation { get; private set; }
    public IReadOnlyList<TenantBackupDto> CloneSources { get; private set; } = [];

    public IndexModel(ICurrentTenant currentTenant, ITenantRestoreAppService restore,
        ITenantOperationalDataResetAppService resetService, IPermissionChecker permissionChecker)
    {
        _currentTenant = currentTenant;
        _restore = restore;
        _resetService = resetService;
        _permissionChecker = permissionChecker;
    }

    public async Task OnGetAsync(Guid? operationId = null)
    {
        IsHost = !_currentTenant.Id.HasValue;
        CanResetOperationalData = !IsHost && await _permissionChecker.IsGrantedAsync(NoxStockPermissions.Settings.Edit);
        if (!IsHost) return;
        CloneSources = await _restore.GetCloneSourcesAsync();
        if (operationId.HasValue) CloneOperation = await _restore.GetCloneOperationAsync(operationId.Value);
    }

    public async Task<IActionResult> OnPostResetDataAsync()
    {
        try
        {
            var result = await _resetService.ResetAsync(ResetInput);
            TempData["TenantDataResetSummary"] =
                $"{result.TotalCount} operasyon kaydı temizlendi. Ürün: {result.ProductCount}, belge: {result.DocumentCount}, cari: {result.BusinessPartnerCount}, hesap: {result.AccountCount}.";
        }
        catch (BusinessException exception)
        {
            TempData["TenantDataResetError"] = exception.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCloneAsync()
    {
        IsHost = !_currentTenant.Id.HasValue;
        if (!ValidateCloneInput())
        {
            CloneSources = IsHost ? await _restore.GetCloneSourcesAsync() : [];
            return Page();
        }
        CloneOperation = await _restore.StartCloneAsync(new StartTenantCloneInputDto
        {
            SourceBackupId = SourceBackupId, Password = RestorePassword, TargetTenantName = TargetTenantName,
            TargetTenantCode = TargetTenantCode, AdminEmailAddress = AdminEmailAddress,
            AdminPassword = AdminPassword, OperationKey = OperationKey
        });
        return Page();
    }

    private bool ValidateCloneInput()
    {
        if (!IsHost) ModelState.AddModelError(string.Empty, "Yeni tenant clone işlemi yalnız host yöneticisi tarafından yapılabilir.");
        if (SourceBackupId == Guid.Empty) ModelState.AddModelError(nameof(SourceBackupId), "Süresi geçmemiş bir tenant yedeği seçin.");
        if (string.IsNullOrEmpty(RestorePassword) || RestorePassword.Length < 12) ModelState.AddModelError(nameof(RestorePassword), "Paket parolası en az 12 karakter olmalıdır.");
        if (string.IsNullOrWhiteSpace(TargetTenantName)) ModelState.AddModelError(nameof(TargetTenantName), "Hedef işletme adını girin.");
        if (string.IsNullOrWhiteSpace(TargetTenantCode)) ModelState.AddModelError(nameof(TargetTenantCode), "Hedef tenant kodunu girin.");
        if (string.IsNullOrWhiteSpace(AdminEmailAddress)) ModelState.AddModelError(nameof(AdminEmailAddress), "Yeni yönetici e-postasını girin.");
        if (string.IsNullOrEmpty(AdminPassword) || AdminPassword.Length < 8) ModelState.AddModelError(nameof(AdminPassword), "Yeni yönetici parolası gereklidir.");
        return ModelState.IsValid;
    }
}
