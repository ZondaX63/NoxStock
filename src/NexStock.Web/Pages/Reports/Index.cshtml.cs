using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NexStock.Reports;

namespace NexStock.Web.Pages.Reports;

public class IndexModel : NexStockPageModel
{
    private readonly IReportingAppService _reportingAppService;

    [BindProperty(SupportsGet = true)]
    public DateTime? DateFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? DateTo { get; set; }

    public ReportSnapshotDto Report { get; private set; } = new();

    public IndexModel(IReportingAppService reportingAppService)
    {
        _reportingAppService = reportingAppService;
    }

    public async Task OnGetAsync()
    {
        DateTo ??= DateTime.Today;
        DateFrom ??= DateTo.Value.AddDays(-29);
        Report = await _reportingAppService.GetSnapshotAsync(new ReportRequestDto
        {
            DateFrom = DateFrom,
            DateTo = DateTo
        });
    }

    public string Money(decimal value) => value.ToString("N2", CultureInfo.CurrentCulture) + " TL";
    public string Quantity(decimal value) => value.ToString("N2", CultureInfo.CurrentCulture);
}
