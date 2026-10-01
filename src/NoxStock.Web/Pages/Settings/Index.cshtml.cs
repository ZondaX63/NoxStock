using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NoxStock.Accounts;
using NoxStock.Licensing;
using NoxStock.Settings;
using Volo.Abp.Application.Dtos;
using Volo.Abp.MultiTenancy;

namespace NoxStock.Web.Pages.Settings;

public class IndexModel : NoxStockPageModel
{
    [BindProperty]
    public CompanySettingsDto Settings { get; set; } = new();

    public List<SelectListItem> CashAccounts { get; private set; } = new();
    public List<SelectListItem> BankAccounts { get; private set; } = new();
    public TenantLicenseAccessDto? License { get; private set; }

    private readonly ICompanySettingsAppService _companySettingsAppService;
    private readonly IAccountAppService _accountAppService;
    private readonly ITenantLicenseAccessChecker _licenseAccessChecker;
    private readonly ICurrentTenant _currentTenant;

    public IndexModel(
        ICompanySettingsAppService companySettingsAppService,
        IAccountAppService accountAppService,
        ITenantLicenseAccessChecker licenseAccessChecker,
        ICurrentTenant currentTenant)
    {
        _companySettingsAppService = companySettingsAppService;
        _accountAppService = accountAppService;
        _licenseAccessChecker = licenseAccessChecker;
        _currentTenant = currentTenant;
    }

    public async Task OnGetAsync()
    {
        Settings = await _companySettingsAppService.GetAsync();
        await LoadAccountsAsync();
        await LoadLicenseAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        NormalizePostedCurrency();
        TryNormalizePostedDecimal("Settings.DefaultVatRate", value => Settings.DefaultVatRate = value);
        if (!ModelState.IsValid)
        {
            await LoadAccountsAsync();
            await LoadLicenseAsync();
            return Page();
        }

        Settings = await _companySettingsAppService.UpdateAsync(Settings);
        TempData["SettingsSaved"] = "Ayarlar kaydedildi.";
        return RedirectToPage();
    }

    private void NormalizePostedCurrency()
    {
        var submitted = Settings.DefaultCurrency?.Trim();
        if (string.IsNullOrWhiteSpace(submitted))
        {
            return;
        }

        // Older cached page markup could submit the display label (for example
        // "TRY — Türk lirası") instead of the ISO value. Persist only supported
        // ISO currency codes; unknown values retain normal model validation.
        var candidate = submitted.Split([' ', '—', '-'], StringSplitOptions.RemoveEmptyEntries)[0]
            .ToUpperInvariant();
        if (candidate is not ("TRY" or "USD" or "EUR"))
        {
            return;
        }

        Settings.DefaultCurrency = candidate;
        ModelState.Remove("Settings.DefaultCurrency");
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

}
