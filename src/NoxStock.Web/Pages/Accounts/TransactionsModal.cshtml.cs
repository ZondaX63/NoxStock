using System;
using Microsoft.AspNetCore.Mvc;

namespace NoxStock.Web.Pages.Accounts;

public class TransactionsModalModel : NoxStockPageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid AccountId { get; set; }

    public void OnGet()
    {
    }
}
