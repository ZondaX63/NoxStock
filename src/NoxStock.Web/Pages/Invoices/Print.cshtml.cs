using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NoxStock.Documents;
using NoxStock.Settings;

namespace NoxStock.Web.Pages.Invoices;

public class PrintModel : NoxStockPageModel
{
    private readonly IDocumentAppService _documentAppService;
    private readonly ICompanySettingsAppService _companySettingsAppService;

    public DocumentDto Document { get; private set; } = new();
    public CompanySettingsDto Company { get; private set; } = new();
    public bool UsesImmutableSnapshot { get; private set; }
    public bool UsesLegacyFinalizedFallback { get; private set; }

    public PrintModel(
        IDocumentAppService documentAppService,
        ICompanySettingsAppService companySettingsAppService)
    {
        _documentAppService = documentAppService;
        _companySettingsAppService = companySettingsAppService;
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            return NotFound();
        }

        Document = await _documentAppService.GetAsync(id);
        if (Document.Type is not (DocumentType.SaleInvoice or DocumentType.PurchaseInvoice or
            DocumentType.SaleOrder or DocumentType.PurchaseOrder or DocumentType.Offer))
        {
            return NotFound();
        }

        UsesImmutableSnapshot = Document.PrintSnapshotCapturedAt.HasValue && Document.Status is
            DocumentStatus.Approved or DocumentStatus.PartiallyPaid or DocumentStatus.Paid or DocumentStatus.Cancelled;
        UsesLegacyFinalizedFallback = !UsesImmutableSnapshot && Document.Status is
            DocumentStatus.Approved or DocumentStatus.PartiallyPaid or DocumentStatus.Paid or DocumentStatus.Cancelled;

        Company = UsesImmutableSnapshot
            ? new CompanySettingsDto
            {
                CompanyName = Document.CompanyNameSnapshot ?? string.Empty,
                Address = Document.CompanyAddressSnapshot ?? string.Empty,
                Phone = Document.CompanyPhoneSnapshot ?? string.Empty,
                Email = Document.CompanyEmailSnapshot ?? string.Empty,
                TaxOffice = Document.CompanyTaxOfficeSnapshot ?? string.Empty,
                TaxNumber = Document.CompanyTaxNumberSnapshot ?? string.Empty
            }
            : await _companySettingsAppService.GetAsync();
        return Page();
    }

    public string GetDocumentTitle() => Document.Type switch
    {
        DocumentType.SaleInvoice => "SATIŞ FATURASI",
        DocumentType.PurchaseInvoice => "ALIŞ FATURASI",
        DocumentType.SaleOrder => "SATIŞ SİPARİŞİ",
        DocumentType.PurchaseOrder => "ALIŞ SİPARİŞİ",
        DocumentType.Offer => "TEKLİF",
        _ => "TİCARİ BELGE"
    };

    public string GetPartnerLabel() => Document.Type == DocumentType.PurchaseInvoice ||
        Document.Type == DocumentType.PurchaseOrder
            ? "Tedarikçi"
            : "Müşteri";

    public string GetStatus() => Document.Status switch
    {
        DocumentStatus.Draft => "Taslak",
        DocumentStatus.Sent => "Gönderildi",
        DocumentStatus.Approved => "Onaylandı",
        DocumentStatus.PartiallyPaid => "Kısmi ödendi",
        DocumentStatus.Paid => "Ödendi",
        DocumentStatus.Cancelled => "İptal",
        DocumentStatus.Completed => "Tamamlandı / dönüştürüldü",
        _ => Document.Status.ToString()
    };
}
