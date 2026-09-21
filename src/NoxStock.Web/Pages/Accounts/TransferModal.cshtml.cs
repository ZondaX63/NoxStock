using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NoxStock.Accounts;

namespace NoxStock.Web.Pages.Accounts;

public class TransferModalModel : NoxStockPageModel
{
    private readonly IAccountAppService _accountAppService;

    [BindProperty]
    public TransferDto Transfer { get; set; } = new();

    public List<AccountDto> Accounts { get; set; } = new();

    public TransferModalModel(IAccountAppService accountAppService)
    {
        _accountAppService = accountAppService;
    }

    public async Task OnGetAsync()
    {
        var accountLookup = await _accountAppService.GetListAsync(new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto { MaxResultCount = 1000 });
        Accounts = accountLookup.Items.Where(x => x.IsActive).OrderBy(x => x.Name).ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        TryNormalizePostedDecimal("Transfer.Amount", value => Transfer.Amount = value);
        if (!ModelState.IsValid)
        {
            await OnGetAsync();
            return Page();
        }

        await _accountAppService.TransferAsync(Transfer);
        return NoContent();
    }
}
