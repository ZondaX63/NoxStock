using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NexStock.Accounts;

namespace NexStock.Web.Pages.Accounts;

public class TransferModalModel : NexStockPageModel
{
    private readonly IAccountAppService _accountAppService;

    [BindProperty]
    public TransferDto Transfer { get; set; } = new();

    public List<SelectListItem> Accounts { get; set; } = new();

    public TransferModalModel(IAccountAppService accountAppService)
    {
        _accountAppService = accountAppService;
    }

    public async Task OnGetAsync()
    {
        var accountLookup = await _accountAppService.GetListAsync(new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto { MaxResultCount = 1000 });
        Accounts = accountLookup.Items.Select(x => new SelectListItem(x.Name + " (" + x.Currency + " - Bakiye: " + x.Balance + ")", x.Id.ToString())).ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _accountAppService.TransferAsync(Transfer);
        return NoContent();
    }
}
