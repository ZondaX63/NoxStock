using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NoxStock.Accounts;

namespace NoxStock.Web.Pages.Accounts;

public class AddTransactionModalModel : NoxStockPageModel
{
    private readonly IAccountAppService _accountAppService;

    [BindProperty]
    public AddTransactionDto Transaction { get; set; } = new();

    public List<SelectListItem> Accounts { get; set; } = new();
    public List<SelectListItem> TransactionTypes { get; set; } = new();

    public AddTransactionModalModel(IAccountAppService accountAppService)
    {
        _accountAppService = accountAppService;
    }

    public async Task OnGetAsync(Guid? accountId)
    {
        var accountLookup = await _accountAppService.GetListAsync(new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto { MaxResultCount = 1000 });
        Accounts = accountLookup.Items.Where(x => x.IsActive).Select(x => new SelectListItem(x.Name + " (" + x.Currency + ")", x.Id.ToString())).ToList();
        
        if (accountId.HasValue)
        {
            Transaction.AccountId = accountId.Value;
        }

        TransactionTypes = new List<SelectListItem>
        {
            new SelectListItem("Para girişi", ((int)AccountTransactionType.Income).ToString()),
            new SelectListItem("Para çıkışı", ((int)AccountTransactionType.Expense).ToString())
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        TryNormalizePostedDecimal("Transaction.Amount", value => Transaction.Amount = value);
        if (!ModelState.IsValid)
        {
            await OnGetAsync(Transaction.AccountId);
            return Page();
        }

        await _accountAppService.AddTransactionAsync(Transaction);
        return NoContent();
    }
}
