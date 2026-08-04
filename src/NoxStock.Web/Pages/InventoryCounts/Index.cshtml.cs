using Microsoft.AspNetCore.Authorization;
using NoxStock.Permissions;
using System.Threading.Tasks;

namespace NoxStock.Web.Pages.InventoryCounts;

[Authorize(NoxStockPermissions.InventoryCounts.Default)]
public class IndexModel : NoxStockPageModel
{
    private readonly IAuthorizationService _authorizationService;

    public bool CanComplete { get; private set; }
    public bool CanQuickCreateProduct { get; private set; }

    public IndexModel(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public async Task OnGetAsync()
    {
        CanComplete = (await _authorizationService.AuthorizeAsync(User, NoxStockPermissions.InventoryCounts.Complete)).Succeeded;
        CanQuickCreateProduct = (await _authorizationService.AuthorizeAsync(User, NoxStockPermissions.Products.Create)).Succeeded;
    }
}
