using System;
using System.Globalization;
using System.Linq;
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

    public string FormatCompactMoney(decimal value)
    {
        return value >= 1000m
            ? value.ToString("0.#,K", TurkishCulture) + " TL"
            : value.ToString("0", TurkishCulture) + " TL";
    }

    public string FormatQuantity(decimal value)
    {
        return value.ToString("0.##", TurkishCulture);
    }

    public string GetSalesTrendHeight(decimal value)
    {
        var maximum = Summary.SalesTrend.Select(x => x.Total).DefaultIfEmpty().Max();
        if (maximum <= 0 || value <= 0)
        {
            return "0%";
        }

        var percentage = Math.Max(8m, Math.Round(value / maximum * 100m, 0));
        return percentage.ToString(CultureInfo.InvariantCulture) + "%";
    }
}
