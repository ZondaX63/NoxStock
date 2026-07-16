using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NoxStock.BusinessPartners;
using NoxStock.Documents;
using NoxStock.Products;
using NoxStock.Settings;

namespace NoxStock.Web.Pages.Invoices;

public class CreateModel : NoxStockPageModel
{
    private static readonly string[] AcceptedDateFormats = ["yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy"];

    [BindProperty(SupportsGet = true)]
    public DocumentType Type { get; set; } = DocumentType.SaleInvoice;

    [BindProperty]
    public CreateUpdateDocumentDto Invoice { get; set; } = new();

    public List<SelectListItem> BusinessPartners { get; set; } = new();
    public List<SelectListItem> Products { get; set; } = new();
    public decimal DefaultVatRate { get; private set; } = 20;
    public bool UsesSalesPricing => Type is DocumentType.SaleInvoice or DocumentType.Offer or DocumentType.SaleOrder;

    private readonly IBusinessPartnerAppService _businessPartnerAppService;
    private readonly IDocumentAppService _documentAppService;
    private readonly IProductAppService _productAppService;
    private readonly ICompanySettingsAppService _companySettingsAppService;

    public CreateModel(
        IBusinessPartnerAppService businessPartnerAppService,
        IDocumentAppService documentAppService,
        IProductAppService productAppService,
        ICompanySettingsAppService companySettingsAppService)
    {
        _businessPartnerAppService = businessPartnerAppService;
        _documentAppService = documentAppService;
        _productAppService = productAppService;
        _companySettingsAppService = companySettingsAppService;
    }

    public async Task OnGetAsync()
    {
        var settings = await _companySettingsAppService.GetAsync();
        DefaultVatRate = settings.DefaultVatRate;
        Invoice = new CreateUpdateDocumentDto
        {
            DocNumber = await _documentAppService.GetSuggestedNumberAsync(Type),
            Date = DateTime.Now,
            DueDate = DateTime.Now.AddDays(30),
            Type = Type,
            Currency = settings.DefaultCurrency,
            Items = new List<CreateUpdateDocumentItemDto>() // Starts empty
        };

        await LoadLookupsAsync(Type);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        NormalizeInvoiceDates();
        Type = Invoice.Type;
        DefaultVatRate = (await _companySettingsAppService.GetAsync()).DefaultVatRate;
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync(Invoice.Type);
            return Page();
        }

        if (Invoice.Items == null || Invoice.Items.Count == 0)
        {
            ModelState.AddModelError("", "En az bir ürün eklemelisiniz.");
            await LoadLookupsAsync(Invoice.Type);
            return Page();
        }

        // Fetch missing prices if they are 0
        foreach(var item in Invoice.Items)
        {
            if(item.UnitPrice <= 0)
            {
                var product = await _productAppService.GetAsync(item.ProductId);
                item.UnitPrice = Invoice.Type is DocumentType.SaleInvoice or DocumentType.Offer or DocumentType.SaleOrder
                    ? product.SalePrice
                    : product.PurchasePrice;
            }
        }

        var document = await _documentAppService.CreateAsync(Invoice);

        return RedirectToPage(GetRedirectPage(Invoice.Type));
    }

    private void NormalizeInvoiceDates()
    {
        NormalizeDateInput("Invoice.Date", required: true, value => Invoice.Date = value);
        NormalizeDateInput("Invoice.DueDate", required: false, value => Invoice.DueDate = value);
    }

    private void NormalizeDateInput(string key, bool required, Action<DateTime> assign)
    {
        if (!Request.HasFormContentType)
        {
            return;
        }

        var rawValue = Request.Form[key].ToString();
        ModelState.Remove(key);

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            if (required)
            {
                ModelState.AddModelError(key, "Tarih zorunludur.");
            }

            return;
        }

        if (DateTime.TryParseExact(
                rawValue,
                AcceptedDateFormats,
                CultureInfo.GetCultureInfo("tr-TR"),
                DateTimeStyles.AllowWhiteSpaces,
                out var parsedDate))
        {
            assign(parsedDate);
            return;
        }

        ModelState.AddModelError(key, "Lütfen geçerli bir tarih girin.");
    }

    private async Task LoadLookupsAsync(DocumentType type)
    {
        var targetType = type is DocumentType.PurchaseInvoice or DocumentType.PurchaseOrder
            ? BusinessPartnerType.Supplier
            : BusinessPartnerType.Customer;
        var bpLookup = await _businessPartnerAppService.GetListAsync(new BusinessPartnerGetListInput
        {
            MaxResultCount = 1000,
            Type = targetType
        });
        BusinessPartners = bpLookup.Items.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList();
        BusinessPartners.Insert(0, new SelectListItem("Seçiniz...", string.Empty));

        var productLookup = await _productAppService.GetListAsync(new ProductGetListInput
        {
            MaxResultCount = 1000
        });
        // We might want to pass price info to the UI to update inputs on select. We can do this via data- attributes.
        Products = productLookup.Items.Select(x => new SelectListItem
        {
            Text = $"{x.Name} ({x.Quantity} {x.Unit})",
            Value = x.Id.ToString()
        }).ToList();
        Products.Insert(0, new SelectListItem("Ürün seçiniz...", string.Empty));
    }

    public string GetDocumentTitle()
    {
        return Type switch
        {
            DocumentType.SaleInvoice => "Yeni Satış Faturası",
            DocumentType.PurchaseInvoice => "Yeni Alış Faturası",
            DocumentType.Offer => "Yeni Teklif",
            DocumentType.SaleOrder => "Yeni Satış Siparişi",
            DocumentType.PurchaseOrder => "Yeni Alış Siparişi",
            _ => "Yeni Belge"
        };
    }

    public string GetPartnerLabel()
    {
        return Type is DocumentType.PurchaseInvoice or DocumentType.PurchaseOrder ? "Tedarikçi" : "Müşteri";
    }

    public string GetRedirectPage(DocumentType type)
    {
        return type == DocumentType.Offer ? "/Offers/Index" :
            type is DocumentType.SaleOrder or DocumentType.PurchaseOrder ? "/Orders/Index" :
            "/Invoices/Index";
    }

}
