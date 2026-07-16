using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NoxStock.Dashboard;

namespace NoxStock.Web.Pages.Dashboard;

public class IndexModel : NoxStockPageModel
{
    private readonly IDashboardAppService _dashboardAppService;

    public DashboardSummaryDto Summary { get; private set; } = new();

    public IndexModel(IDashboardAppService dashboardAppService)
    {
        _dashboardAppService = dashboardAppService;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (CurrentUser.TenantId == null)
        {
            return Redirect("/TenantManagement/Tenants");
        }

        Summary = await _dashboardAppService.GetSummaryAsync();
        return Page();
    }

    public string FormatMoney(decimal value)
    {
        return value.ToString("N2", CultureInfo.CurrentCulture) + " ₺";
    }

    public string FormatCompactMoney(decimal value)
    {
        return value >= 1000m
            ? value.ToString("0.#,K", CultureInfo.CurrentCulture) + " ₺"
            : value.ToString("0", CultureInfo.CurrentCulture) + " ₺";
    }

    public string FormatQuantity(decimal value)
    {
        return value.ToString("0.##", CultureInfo.CurrentCulture);
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
