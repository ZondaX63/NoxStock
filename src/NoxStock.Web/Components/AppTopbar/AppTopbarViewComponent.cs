using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Users;
using NoxStock.Notifications;
using NoxStock.Permissions;
using NoxStock.Licensing;

namespace NoxStock.Web.Components.AppTopbar;

public class AppTopbarViewComponent : AbpViewComponent
{
    private readonly ICurrentUser _currentUser;
    private readonly IOperationalAlertsAppService _alertsAppService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ITenantLicenseAccessChecker _licenseAccessChecker;

    public AppTopbarViewComponent(
        ICurrentUser currentUser,
        IOperationalAlertsAppService alertsAppService,
        IAuthorizationService authorizationService,
        ITenantLicenseAccessChecker licenseAccessChecker)
    {
        _currentUser = currentUser;
        _alertsAppService = alertsAppService;
        _authorizationService = authorizationService;
        _licenseAccessChecker = licenseAccessChecker;
    }

    public virtual async Task<IViewComponentResult> InvokeAsync()
    {
        var canReadNotifications = _currentUser.IsAuthenticated &&
            (await _authorizationService.IsGrantedAsync(NoxStockPermissions.Notifications.Default));
        var summary = canReadNotifications
            ? await _alertsAppService.GetSummaryAsync()
            : new OperationalAlertsSummaryDto();
        var model = new AppTopbarViewModel
        {
            AlertsSummary = summary,
            License = _currentUser.TenantId.HasValue
                ? await _licenseAccessChecker.GetCurrentAsync()
                : null
        };
        return View("~/Components/AppTopbar/Default.cshtml", model);
    }
}
