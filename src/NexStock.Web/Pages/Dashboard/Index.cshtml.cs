using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NexStock.Dashboard;

namespace NexStock.Web.Pages.Dashboard;

public class IndexModel : PageModel
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
    private readonly IDashboardAppService _dashboardAppService;

    public DashboardSummaryDto Summary { get; private set; } = new();

    public IndexModel(IDashboardAppService dashboardAppService)
    {
        _dashboardAppService = dashboardAppService;
    }

    public async Task OnGetAsync()
    {
        Summary = await _dashboardAppService.GetSummaryAsync();
    }

    public string FormatMoney(decimal value)
    {
        return value.ToString("C2", TurkishCulture);
    }
}
