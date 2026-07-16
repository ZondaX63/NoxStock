using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Users;
using NoxStock.Notifications;
using NoxStock.Permissions;

namespace NoxStock.Web.Components.AppTopbar;

public class AppTopbarViewComponent : AbpViewComponent
{
    private readonly ICurrentUser _currentUser;
    private readonly IOperationalAlertsAppService _alertsAppService;
    private readonly IAuthorizationService _authorizationService;

    public AppTopbarViewComponent(
        ICurrentUser currentUser,
        IOperationalAlertsAppService alertsAppService,
        IAuthorizationService authorizationService)
    {
        _currentUser = currentUser;
        _alertsAppService = alertsAppService;
        _authorizationService = authorizationService;
    }

    public virtual async Task<IViewComponentResult> InvokeAsync()
    {
        var canReadNotifications = _currentUser.IsAuthenticated &&
            (await _authorizationService.IsGrantedAsync(NoxStockPermissions.Notifications.Default));
        var summary = canReadNotifications
            ? await _alertsAppService.GetSummaryAsync()
            : new OperationalAlertsSummaryDto();
        return View("~/Components/AppTopbar/Default.cshtml", summary);
    }
}
