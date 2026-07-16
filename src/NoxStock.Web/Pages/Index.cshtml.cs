using Microsoft.AspNetCore.Mvc;

namespace NoxStock.Web.Pages;

public class IndexModel : NoxStockPageModel
{
    public IActionResult OnGet()
    {
        return RedirectToPage("/Dashboard/Index");
    }
}
