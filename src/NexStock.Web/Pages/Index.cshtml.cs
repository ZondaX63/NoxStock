using Microsoft.AspNetCore.Mvc;

namespace NexStock.Web.Pages;

public class IndexModel : NexStockPageModel
{
    public IActionResult OnGet()
    {
        return RedirectToPage("/Dashboard/Index");
    }
}
