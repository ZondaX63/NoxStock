using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NoxStock.Web.Pages.Account;

public class RegisterDisabledModel : PageModel
{
    public IActionResult OnGet()
    {
        return RedirectToLogin();
    }

    public IActionResult OnPost()
    {
        return RedirectToLogin();
    }

    private IActionResult RedirectToLogin()
    {
        return LocalRedirect(Request.PathBase.Add("/Account/Login").Value!);
    }
}
