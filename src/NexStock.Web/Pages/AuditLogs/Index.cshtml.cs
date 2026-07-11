using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NexStock.AuditLogs;

namespace NexStock.Web.Pages.AuditLogs;

public class IndexModel : NexStockPageModel
{
    private readonly IAuditLogAppService _auditLogAppService;

    [BindProperty(SupportsGet = true)]
    public DateTime? DateFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? DateTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? UserName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? HttpMethod { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? HasException { get; set; }

    public AuditLogSnapshotDto Snapshot { get; private set; } = new();

    public IndexModel(IAuditLogAppService auditLogAppService)
    {
        _auditLogAppService = auditLogAppService;
    }

    public async Task OnGetAsync()
    {
        DateFrom ??= DateTime.Today.AddDays(-30);
        DateTo ??= DateTime.Today;
        Snapshot = await _auditLogAppService.GetListAsync(new AuditLogRequestDto
        {
            DateFrom = DateFrom,
            DateTo = DateTo,
            UserName = UserName,
            HttpMethod = HttpMethod,
            HasException = HasException,
            MaxResultCount = 100
        });
    }

    public string FormatDate(DateTime value) => value.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("tr-TR"));
}
