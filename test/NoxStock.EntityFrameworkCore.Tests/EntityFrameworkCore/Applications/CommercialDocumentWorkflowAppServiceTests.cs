using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.BusinessPartners;
using NoxStock.Documents;
using NoxStock.Products;
using Shouldly;
using Xunit;

namespace NoxStock.EntityFrameworkCore.Applications;

public class CommercialDocumentWorkflowAppServiceTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly IBusinessPartnerAppService _businessPartnerAppService;
    private readonly IProductAppService _productAppService;
    private readonly IDocumentAppService _documentAppService;
    private readonly ICommercialDocumentWorkflowAppService _workflowAppService;

    public CommercialDocumentWorkflowAppServiceTests()
    {
        _businessPartnerAppService = ServiceProvider.GetRequiredService<IBusinessPartnerAppService>();
        _productAppService = ServiceProvider.GetRequiredService<IProductAppService>();
        _documentAppService = ServiceProvider.GetRequiredService<IDocumentAppService>();
        _workflowAppService = ServiceProvider.GetRequiredService<ICommercialDocumentWorkflowAppService>();
    }

    [Fact]
    public async Task Should_Convert_Offer_To_Sales_Order_And_Then_Invoice_Preserving_Source_And_Totals()
    {
        var customer = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = "Workflow Customer",
            Type = BusinessPartnerType.Customer,
            Currency = "TRY"
        });
        var product = await CreateProductAsync("WORKFLOW-SALE-001", 70, 125);

        var offer = await CreateDocumentAsync(customer.Id, product.Id, "OFR-001", DocumentType.Offer, 2, 125, 10, 20);
        var sentOffer = await _workflowAppService.SendOfferAsync(offer.Id);
        sentOffer.Status.ShouldBe(DocumentStatus.Sent);

        var salesOrder = await _workflowAppService.ConvertOfferToOrderAsync(offer.Id);
        salesOrder.Type.ShouldBe(DocumentType.SaleOrder);
        salesOrder.Status.ShouldBe(DocumentStatus.Draft);
        salesOrder.SourceDocumentId.ShouldBe(offer.Id);
        salesOrder.GrandTotal.ShouldBe(270);
        salesOrder.Items.Count.ShouldBe(1);
        salesOrder.Items[0].Quantity.ShouldBe(2);

        var completedOffer = await _documentAppService.GetAsync(offer.Id);
        completedOffer.Status.ShouldBe(DocumentStatus.Completed);

        var sentOrder = await _workflowAppService.SendOrderAsync(salesOrder.Id);
        sentOrder.Status.ShouldBe(DocumentStatus.Sent);

        var salesInvoice = await _workflowAppService.ConvertOrderToInvoiceAsync(salesOrder.Id);
        salesInvoice.Type.ShouldBe(DocumentType.SaleInvoice);
        salesInvoice.Status.ShouldBe(DocumentStatus.Draft);
        salesInvoice.SourceDocumentId.ShouldBe(salesOrder.Id);
        salesInvoice.GrandTotal.ShouldBe(salesOrder.GrandTotal);
        salesInvoice.Items.Single().ProductId.ShouldBe(product.Id);

        var completedOrder = await _documentAppService.GetAsync(salesOrder.Id);
        completedOrder.Status.ShouldBe(DocumentStatus.Completed);
    }

    [Fact]
    public async Task Should_Convert_Purchase_Order_To_Purchase_Invoice_Preserving_Source_And_Items()
    {
        var supplier = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = "Workflow Supplier",
            Type = BusinessPartnerType.Supplier,
            Currency = "TRY"
        });
        var product = await CreateProductAsync("WORKFLOW-PURCHASE-001", 80, 140);
        var order = await CreateDocumentAsync(supplier.Id, product.Id, "ORD-001", DocumentType.PurchaseOrder, 3, 80, 0, 0);

        await _workflowAppService.SendOrderAsync(order.Id);
        var invoice = await _workflowAppService.ConvertOrderToInvoiceAsync(order.Id);

        invoice.Type.ShouldBe(DocumentType.PurchaseInvoice);
        invoice.SourceDocumentId.ShouldBe(order.Id);
        invoice.GrandTotal.ShouldBe(240);
        invoice.Items.Single().Quantity.ShouldBe(3);
        invoice.Items.Single().UnitPrice.ShouldBe(80);

        var completedOrder = await _documentAppService.GetAsync(order.Id);
        completedOrder.Status.ShouldBe(DocumentStatus.Completed);
    }

    private async Task<DocumentDto> CreateDocumentAsync(
        Guid businessPartnerId,
        Guid productId,
        string docNumber,
        DocumentType type,
        decimal quantity,
        decimal unitPrice,
        decimal discountRate,
        decimal vatRate)
    {
        return await _documentAppService.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = businessPartnerId,
            DocNumber = docNumber,
            Type = type,
            Status = DocumentStatus.Draft,
            Currency = "TRY",
            ExchangeRate = 1,
            Date = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(14),
            Items =
            {
                new CreateUpdateDocumentItemDto
                {
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    DiscountRate = discountRate,
                    VatRate = vatRate
                }
            }
        });
    }

    private async Task<ProductDto> CreateProductAsync(string sku, decimal purchasePrice, decimal salePrice)
    {
        return await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = sku,
            Sku = sku,
            Barcode = string.Empty,
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = "Workflow",
            PurchasePrice = purchasePrice,
            SalePrice = salePrice,
            Quantity = 10,
            CriticalStockLevel = 1,
            TrackStock = true,
            Currency = "TRY",
            PurchaseCurrency = "TRY",
            SaleCurrency = "TRY"
        });
    }
}
