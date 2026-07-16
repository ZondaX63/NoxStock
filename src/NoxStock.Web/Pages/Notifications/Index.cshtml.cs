using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NoxStock.Notifications;

namespace NoxStock.Web.Pages.Notifications;

public class IndexModel : PageModel
{
    private readonly IOperationalAlertsAppService _alertsAppService;

    public OperationalAlertsSummaryDto Summary { get; private set; } = new();

    public IndexModel(IOperationalAlertsAppService alertsAppService)
    {
        _alertsAppService = alertsAppService;
    }

    public async Task OnGetAsync()
    {
        Summary = await _alertsAppService.GetSummaryAsync();
    }
}
