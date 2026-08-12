using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NoxStock.Accounts;

namespace NoxStock.Web.Pages.Accounts;

public class CancelTransactionModalModel : NoxStockPageModel
{
    private readonly IAccountAppService _accountAppService;
    private readonly IAccountTransactionAppService _transactionAppService;

    [BindProperty(SupportsGet = true)]
    public Guid TransactionId { get; set; }

    [BindProperty]
    public CancelAccountTransactionDto Cancellation { get; set; } = new();

    public AccountTransactionDto Transaction { get; private set; } = new();

    public CancelTransactionModalModel(IAccountAppService accountAppService, IAccountTransactionAppService transactionAppService)
    {
        _accountAppService = accountAppService;
        _transactionAppService = transactionAppService;
    }

    public async Task OnGetAsync()
    {
        Transaction = await _transactionAppService.GetAsync(TransactionId);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _accountAppService.CancelTransactionAsync(TransactionId, Cancellation);
        return NoContent();
    }
}
