using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NoxStock.Accounts;

namespace NoxStock.Web.Pages.Accounts;

public class EditModalModel : NoxStockPageModel
{
    [HiddenInput]
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public CreateUpdateAccountDto Account { get; set; } = new();

    public List<SelectListItem> AccountTypes { get; set; } = GetAccountTypes();

    private readonly IAccountAppService _accountAppService;

    public EditModalModel(IAccountAppService accountAppService)
    {
        _accountAppService = accountAppService;
    }

    public async Task OnGetAsync()
    {
        var account = await _accountAppService.GetAsync(Id);
        Account = new CreateUpdateAccountDto
        {
            Name = account.Name,
            Type = account.Type,
            Currency = account.Currency,
            Balance = account.Balance,
            IsActive = account.IsActive
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _accountAppService.UpdateAsync(Id, Account);
        return NoContent();
    }

    private static List<SelectListItem> GetAccountTypes()
    {
        return new List<SelectListItem>
        {
            new("Kasa", AccountType.Cash.ToString()),
            new("Banka", AccountType.Bank.ToString()),
            new("Kredi Kartı", AccountType.CreditCard.ToString()),
            new("Personel", AccountType.Personnel.ToString()),
            new("Diğer", AccountType.Other.ToString())
        };
    }
}
