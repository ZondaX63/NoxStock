using NoxStock.Licensing;
using NoxStock.Notifications;

namespace NoxStock.Web.Components.AppTopbar;

public class AppTopbarViewModel
{
    public OperationalAlertsSummaryDto AlertsSummary { get; set; } = new();
    public TenantLicenseAccessDto? License { get; set; }
}
