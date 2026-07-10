using System;
using Microsoft.AspNetCore.Mvc;

namespace NexStock.Web.Pages.Accounts;

public class TransactionsModalModel : NexStockPageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid AccountId { get; set; }

    public void OnGet()
    {
    }
}
