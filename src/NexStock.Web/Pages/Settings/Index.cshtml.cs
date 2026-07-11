using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NexStock.Accounts;
using NexStock.Settings;
using Volo.Abp.Application.Dtos;

namespace NexStock.Web.Pages.Settings;

public class IndexModel : NexStockPageModel
{
    [BindProperty]
    public CompanySettingsDto Settings { get; set; } = new();

    public List<SelectListItem> CashAccounts { get; private set; } = new();
    public List<SelectListItem> BankAccounts { get; private set; } = new();

    private readonly ICompanySettingsAppService _companySettingsAppService;
    private readonly IAccountAppService _accountAppService;

    public IndexModel(
        ICompanySettingsAppService companySettingsAppService,
        IAccountAppService accountAppService)
    {
        _companySettingsAppService = companySettingsAppService;
        _accountAppService = accountAppService;
    }

    public async Task OnGetAsync()
    {
        Settings = await _companySettingsAppService.GetAsync();
        await LoadAccountsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAccountsAsync();
            return Page();
        }

        Settings = await _companySettingsAppService.UpdateAsync(Settings);
        TempData["SettingsSaved"] = "Ayarlar kaydedildi.";
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
}
