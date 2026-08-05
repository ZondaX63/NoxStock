using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NoxStock.AuditLogs;

namespace NoxStock.Web.Pages.AuditLogs;

public class IndexModel : NoxStockPageModel
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

    public bool ShowExceptions
    {
        get => HasException == true;
        set => HasException = value ? true : null;
    }

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

    public string GetActivityText(AuditLogItemDto item)
    {
        var area = item.Url.ToLowerInvariant() switch
        {
            var url when url.Contains("product") => "Ürünler",
            var url when url.Contains("document") || url.Contains("invoice") => "Faturalar",
            var url when url.Contains("pos") || url.Contains("sale") => "Perakende satış",
            var url when url.Contains("business-partner") || url.Contains("customer") || url.Contains("supplier") => "Cari hesaplar",
            var url when url.Contains("inventory-count") => "Stok sayımı",
            var url when url.Contains("account") => "Kasa ve bankalar",
            var url when url.Contains("setting") => "Ayarlar",
            _ => "Uygulama"
        };

        var action = item.HttpMethod.ToUpperInvariant() switch
        {
            "POST" => "işlemi gerçekleştirildi",
            "PUT" or "PATCH" => "kaydı güncellendi",
            "DELETE" => "kaydı silindi",
            "GET" => "ekranı görüntülendi",
            _ => "işlemi gerçekleştirildi"
        };

        return $"{area} {action}";
    }
}
