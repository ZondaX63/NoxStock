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
        if (Document.Type is not (DocumentType.SaleInvoice or DocumentType.PurchaseInvoice))
        {
            return NotFound();
        }

        Company = await _companySettingsAppService.GetAsync();
        return Page();
    }

    public string GetDocumentTitle() => Document.Type == DocumentType.SaleInvoice ? "SATIŞ FATURASI" : "ALIŞ FATURASI";

    public string GetStatus() => Document.Status switch
    {
        DocumentStatus.Draft => "Taslak",
        DocumentStatus.Sent => "Gönderildi",
        DocumentStatus.Approved => "Onaylandı",
        DocumentStatus.PartiallyPaid => "Kısmi ödendi",
        DocumentStatus.Paid => "Ödendi",
        DocumentStatus.Cancelled => "İptal",
        _ => Document.Status.ToString()
    };
}
