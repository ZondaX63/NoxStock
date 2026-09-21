using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NoxStock.Accounts;

namespace NoxStock.Web.Pages.Accounts;

public class CreateModalModel : NoxStockPageModel
{
    [BindProperty]
    public CreateAccountWithOpeningDto Account { get; set; } = new();

    public List<SelectListItem> AccountTypes { get; set; } = GetAccountTypes();

    private readonly IAccountAppService _accountAppService;

    public CreateModalModel(IAccountAppService accountAppService)
    {
        _accountAppService = accountAppService;
    }

    public void OnGet()
    {
        Account = new CreateAccountWithOpeningDto
        {
            Type = AccountType.Cash,
            Currency = "TRY",
            IsActive = true
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        TryNormalizePostedDecimal("Account.OpeningAmount", value => Account.OpeningAmount = value);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        await _accountAppService.CreateWithOpeningAsync(Account);
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
