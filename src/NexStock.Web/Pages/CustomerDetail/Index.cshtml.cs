using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;

namespace NexStock.Web.Pages.CustomerDetail;

public class IndexModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public void OnGet()
    {
    }
}
