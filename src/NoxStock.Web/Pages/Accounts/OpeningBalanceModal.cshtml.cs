using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NoxStock.Accounts;

namespace NoxStock.Web.Pages.Accounts;

public class OpeningBalanceModalModel : NoxStockPageModel
{
    private readonly IAccountAppService _accountAppService;

    [BindProperty]
    public CreateAccountOpeningBalanceDto Opening { get; set; } = new();

    public AccountDto Account { get; private set; } = new();

    public OpeningBalanceModalModel(IAccountAppService accountAppService)
    {
        _accountAppService = accountAppService;
    }

    public async Task OnGetAsync(Guid accountId)
    {
        Account = await _accountAppService.GetAsync(accountId);
        Opening.AccountId = accountId;
        Opening.OpeningDate = Clock.Now;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _accountAppService.CreateOpeningBalanceAsync(Opening);
        return NoContent();
    }
}
