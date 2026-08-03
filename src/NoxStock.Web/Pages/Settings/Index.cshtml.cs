using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NoxStock.Accounts;
using NoxStock.Licensing;
using NoxStock.Permissions;
using NoxStock.Settings;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.MultiTenancy;

namespace NoxStock.Web.Pages.Settings;

public class IndexModel : NoxStockPageModel
{
    [BindProperty]
    public CompanySettingsDto Settings { get; set; } = new();

    [BindProperty]
    public TenantOperationalDataResetInputDto ResetInput { get; set; } = new();

    public List<SelectListItem> CashAccounts { get; private set; } = new();
    public List<SelectListItem> BankAccounts { get; private set; } = new();
    public TenantLicenseAccessDto? License { get; private set; }
    public bool CanResetOperationalData { get; private set; }

    private readonly ICompanySettingsAppService _companySettingsAppService;
    private readonly IAccountAppService _accountAppService;
    private readonly ITenantLicenseAccessChecker _licenseAccessChecker;
    private readonly ITenantOperationalDataResetAppService _tenantOperationalDataResetAppService;
    private readonly IPermissionChecker _permissionChecker;
    private readonly ICurrentTenant _currentTenant;

    public IndexModel(
        ICompanySettingsAppService companySettingsAppService,
        IAccountAppService accountAppService,
        ITenantLicenseAccessChecker licenseAccessChecker,
        ITenantOperationalDataResetAppService tenantOperationalDataResetAppService,
        IPermissionChecker permissionChecker,
        ICurrentTenant currentTenant)
    {
        _companySettingsAppService = companySettingsAppService;
        _accountAppService = accountAppService;
        _licenseAccessChecker = licenseAccessChecker;
        _tenantOperationalDataResetAppService = tenantOperationalDataResetAppService;
        _permissionChecker = permissionChecker;
        _currentTenant = currentTenant;
    }

    public async Task OnGetAsync()
    {
        Settings = await _companySettingsAppService.GetAsync();
        await LoadAccountsAsync();
        await LoadLicenseAsync();
        await LoadResetPermissionAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAccountsAsync();
            await LoadLicenseAsync();
            await LoadResetPermissionAsync();
            return Page();
        }

        Settings = await _companySettingsAppService.UpdateAsync(Settings);
        TempData["SettingsSaved"] = "Ayarlar kaydedildi.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResetDataAsync()
    {
        try
        {
            var result = await _tenantOperationalDataResetAppService.ResetAsync(ResetInput);
            TempData["TenantDataResetSummary"] =
                $"{result.TotalCount} operasyon kaydı temizlendi. Ürün: {result.ProductCount}, belge: {result.DocumentCount}, cari: {result.BusinessPartnerCount}, hesap: {result.AccountCount}.";
        }
        catch (BusinessException exception)
        {
            TempData["TenantDataResetError"] = exception.Message;
        }

        return RedirectToPage();
    }

    private async Task LoadAccountsAsync()
    {
        var accounts = await _accountAppService.GetListAsync(new PagedAndSortedResultRequestDto
        {
            MaxResultCount = 1000,
            Sorting = nameof(AccountDto.Name)
        });

        CashAccounts = accounts.Items
            .Where(x => x.IsActive && x.Type == AccountType.Cash)
            .Select(x => new SelectListItem($"{x.Name} ({x.Currency})", x.Id.ToString()))
            .ToList();
        CashAccounts.Insert(0, new SelectListItem("Seçilmedi", string.Empty));

        BankAccounts = accounts.Items
            .Where(x => x.IsActive && x.Type is AccountType.Bank or AccountType.CreditCard)
            .Select(x => new SelectListItem($"{x.Name} ({x.Currency})", x.Id.ToString()))
            .ToList();
        BankAccounts.Insert(0, new SelectListItem("Seçilmedi", string.Empty));
    }

    private async Task LoadLicenseAsync()
    {
        License = _currentTenant.Id.HasValue
            ? await _licenseAccessChecker.GetCurrentAsync()
            : null;
    }

    private async Task LoadResetPermissionAsync()
    {
        CanResetOperationalData = _currentTenant.Id.HasValue &&
                                  await _permissionChecker.IsGrantedAsync(NoxStockPermissions.Settings.Edit);
    }
}
