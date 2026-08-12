using Microsoft.AspNetCore.Mvc;
using Volo.Abp.MultiTenancy;

namespace NoxStock.Web.Pages;

public class IndexModel : NoxStockPageModel
{
    private readonly ICurrentTenant _currentTenant;

    public IndexModel(ICurrentTenant currentTenant)
    {
        _currentTenant = currentTenant;
    }

    public IActionResult OnGet()
    {
        if (_currentTenant.Id.HasValue)
        {
            return LocalRedirect(Request.PathBase.Add("/Dashboard").Value!);
        }

        return Page();
    }
}
