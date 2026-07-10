using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NexStock.BusinessPartners;
using NexStock.Documents;
using NexStock.Products;
using Volo.Abp.Application.Dtos;

namespace NexStock.Web.Pages.Invoices;

public class CreateModel : NexStockPageModel
{
    [BindProperty(SupportsGet = true)]
    public DocumentType Type { get; set; } = DocumentType.SaleInvoice;

    [BindProperty]
    public CreateUpdateDocumentDto Invoice { get; set; } = new();

    public List<SelectListItem> BusinessPartners { get; set; } = new();
    public List<SelectListItem> Products { get; set; } = new();

    private readonly IBusinessPartnerAppService _businessPartnerAppService;
    private readonly IDocumentAppService _documentAppService;
    private readonly IProductAppService _productAppService;

    public CreateModel(
        IBusinessPartnerAppService businessPartnerAppService,
        IDocumentAppService documentAppService,
        IProductAppService productAppService)
    {
        _businessPartnerAppService = businessPartnerAppService;
        _documentAppService = documentAppService;
        _productAppService = productAppService;
    }

    public async Task OnGetAsync()
    {
        Invoice = new CreateUpdateDocumentDto
        {
            DocNumber = $"{(Type == DocumentType.SaleInvoice ? "SF" : "AF")}-{DateTime.Now:yyyyMMdd-HHmm}",
            Date = DateTime.Now,
            DueDate = DateTime.Now.AddDays(30),
            Type = Type,
            Items = new List<CreateUpdateDocumentItemDto>() // Starts empty
        };

        await LoadLookupsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync();
            return Page();
        }

        if (Invoice.Items == null || Invoice.Items.Count == 0)
        {
            ModelState.AddModelError("", "En az bir ürün eklemelisiniz.");
            await LoadLookupsAsync();
            return Page();
        }

        // Fetch missing prices if they are 0
        foreach(var item in Invoice.Items)
        {
            if(item.UnitPrice <= 0)
            {
                var product = await _productAppService.GetAsync(item.ProductId);
                item.UnitPrice = Invoice.Type == DocumentType.SaleInvoice ? product.SalePrice : product.PurchasePrice;
            }
        }

        var document = await _documentAppService.CreateAsync(Invoice);

        // We can redirect to the invoice detail or index. For now, redirect to Index.
        return RedirectToPage("/Invoices/Index");
    }

    private async Task LoadLookupsAsync()
    {
        var targetType = Type == DocumentType.SaleInvoice ? BusinessPartnerType.Customer : BusinessPartnerType.Supplier;
        var bpLookup = await _businessPartnerAppService.GetListAsync(new BusinessPartnerGetListInput
        {
            MaxResultCount = 1000,
            Type = targetType
        });
        BusinessPartners = bpLookup.Items.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList();
        BusinessPartners.Insert(0, new SelectListItem("Seçiniz...", string.Empty));

        var productLookup = await _productAppService.GetListAsync(new PagedAndSortedResultRequestDto
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
}
