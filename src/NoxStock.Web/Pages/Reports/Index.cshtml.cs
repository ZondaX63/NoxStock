using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using NoxStock.Reports;

namespace NoxStock.Web.Pages.Reports;

public sealed record ReportPagerModel(ReportPageInfoDto Page, string PreviousUrl, string NextUrl);

public class IndexModel : NoxStockPageModel
{
    private readonly IReportingAppService _reportingAppService;

    [BindProperty(SupportsGet = true)]
    public DateTime? DateFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? DateTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public int AccountsPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int BusinessPartnersPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int ProductProfitabilityPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int StocksPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int StockMovementsPage { get; set; } = 1;

    public ReportSnapshotDto Report { get; private set; } = new();

    public IndexModel(IReportingAppService reportingAppService)
    {
        _reportingAppService = reportingAppService;
    }

    public async Task OnGetAsync()
    {
        DateTo ??= DateTime.Today;
        DateFrom ??= DateTo.Value.AddDays(-29);
        AccountsPage = Math.Clamp(AccountsPage, 1, 10_000);
        BusinessPartnersPage = Math.Clamp(BusinessPartnersPage, 1, 10_000);
        ProductProfitabilityPage = Math.Clamp(ProductProfitabilityPage, 1, 10_000);
        StocksPage = Math.Clamp(StocksPage, 1, 10_000);
        StockMovementsPage = Math.Clamp(StockMovementsPage, 1, 10_000);
        Report = await _reportingAppService.GetSnapshotAsync(new ReportRequestDto
        {
            DateFrom = DateFrom,
            DateTo = DateTo,
            PageSize = 25,
            AccountsPage = AccountsPage,
            BusinessPartnersPage = BusinessPartnersPage,
            ProductProfitabilityPage = ProductProfitabilityPage,
            StocksPage = StocksPage,
            StockMovementsPage = StockMovementsPage
        });
        DateFrom = Report.DateFrom;
        DateTo = Report.DateTo;
    }

    public string Money(decimal value) => value.ToString("N2", CultureInfo.CurrentCulture) + " TL";
    public string Quantity(decimal value) => value.ToString("N2", CultureInfo.CurrentCulture);

    public string PageUrl(string section, int pageNumber)
    {
        var values = new RouteValueDictionary
        {
            [nameof(DateFrom)] = DateFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            [nameof(DateTo)] = DateTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            [nameof(AccountsPage)] = AccountsPage,
            [nameof(BusinessPartnersPage)] = BusinessPartnersPage,
            [nameof(ProductProfitabilityPage)] = ProductProfitabilityPage,
            [nameof(StocksPage)] = StocksPage,
            [nameof(StockMovementsPage)] = StockMovementsPage
        };
        values[section] = Math.Max(pageNumber, 1);
        return Url.Page("/Reports/Index", values) ?? "#";
    }
}
