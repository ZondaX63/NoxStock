using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Users;
using NoxStock.Permissions;

namespace NoxStock.Web.Components.AppTopbar;

public class AppTopbarViewComponent : AbpViewComponent
{
    private readonly ICurrentUser _currentUser;
    private readonly IAuthorizationService _authorizationService;

    public AppTopbarViewComponent(
        ICurrentUser currentUser,
        IAuthorizationService authorizationService)
    {
        _currentUser = currentUser;
        _authorizationService = authorizationService;
    }

    public virtual async Task<IViewComponentResult> InvokeAsync()
    {
        var canReadNotifications = _currentUser.IsAuthenticated &&
            _currentUser.TenantId.HasValue &&
            (await _authorizationService.IsGrantedAsync(NoxStockPermissions.Notifications.Default));
        var model = new AppTopbarViewModel { CanReadNotifications = canReadNotifications };
        return View("~/Components/AppTopbar/Default.cshtml", model);
    }
}
