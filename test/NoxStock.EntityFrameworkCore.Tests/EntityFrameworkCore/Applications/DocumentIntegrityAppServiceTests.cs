using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.BusinessPartners;
using NoxStock.Documents;
using NoxStock.Products;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace NoxStock.EntityFrameworkCore.Applications;

public class DocumentIntegrityAppServiceTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly IDocumentAppService _documentAppService;
    private readonly IBusinessPartnerAppService _businessPartnerAppService;
    private readonly IProductAppService _productAppService;

    public DocumentIntegrityAppServiceTests()
    {
        _documentAppService = ServiceProvider.GetRequiredService<IDocumentAppService>();
        _businessPartnerAppService = ServiceProvider.GetRequiredService<IBusinessPartnerAppService>();
        _productAppService = ServiceProvider.GetRequiredService<IProductAppService>();
    }

    [Fact]
    public async Task Should_Force_New_Document_To_Draft_And_Calculate_Totals_On_Server()
    {
        var customer = await CreatePartnerAsync("Integrity Customer", BusinessPartnerType.Customer);
        var product = await CreateProductAsync("INTEGRITY-001");

        var document = await _documentAppService.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = customer.Id,
            DocNumber = "INT-001",
            Type = DocumentType.SaleInvoice,
            Status = DocumentStatus.Paid,
            PaidAmount = 9999,
            Subtotal = 1,
            VatTotal = 1,
            DiscountTotal = 1,
            GrandTotal = 1,
            Currency = "TRY",
            Date = DateTime.UtcNow,
            Items =
            {
                new CreateUpdateDocumentItemDto
                {
                    ProductId = product.Id,
                    Quantity = 2,
                    UnitPrice = 12.3456m,
                    DiscountRate = 10,
                    VatRate = 20
                }
            }
        });

        document.Status.ShouldBe(DocumentStatus.Draft);
        document.PaidAmount.ShouldBe(0);
        document.Subtotal.ShouldBe(24.6912m);
        document.DiscountTotal.ShouldBe(2.4691m);
        document.VatTotal.ShouldBe(4.4444m);
        document.GrandTotal.ShouldBe(26.6665m);
        document.Items.Single().Total.ShouldBe(26.6665m);
    }

    [Fact]
    public async Task Should_Reject_Invalid_Partner_Date_And_Duplicate_Document_Number()
    {
        var customer = await CreatePartnerAsync("Validation Customer", BusinessPartnerType.Customer);
        var supplier = await CreatePartnerAsync("Validation Supplier", BusinessPartnerType.Supplier);
        var product = await CreateProductAsync("INTEGRITY-002");
        var documentDate = DateTime.UtcNow.Date;

        await _documentAppService.CreateAsync(CreateInvoice(customer.Id, product.Id, "INT-002", documentDate));

        await Should.ThrowAsync<BusinessException>(() =>
            _documentAppService.CreateAsync(CreateInvoice(customer.Id, product.Id, "INT-002", documentDate)));

        await Should.ThrowAsync<BusinessException>(() =>
            _documentAppService.CreateAsync(CreateInvoice(supplier.Id, product.Id, "INT-003", documentDate)));

        var dueDateInPast = CreateInvoice(customer.Id, product.Id, "INT-004", documentDate);
        dueDateInPast.DueDate = documentDate.AddDays(-1);
        await Should.ThrowAsync<BusinessException>(() => _documentAppService.CreateAsync(dueDateInPast));
    }

    [Fact]
    public async Task Should_Update_Draft_Document_Items_And_Recalculate_Totals()
    {
        var customer = await CreatePartnerAsync("Draft Edit Customer", BusinessPartnerType.Customer);
        var product = await CreateProductAsync("DRAFT-EDIT-001");
        var document = await _documentAppService.CreateAsync(CreateInvoice(customer.Id, product.Id, "DRAFT-EDIT-001", DateTime.UtcNow.Date));

        var updated = await _documentAppService.UpdateAsync(document.Id, new CreateUpdateDocumentDto
        {
            BusinessPartnerId = customer.Id,
            DocNumber = document.DocNumber,
            Type = DocumentType.SaleInvoice,
            Currency = "TRY",
            Date = DateTime.UtcNow.Date,
            DueDate = DateTime.UtcNow.Date.AddDays(7),
            Items =
            {
                new CreateUpdateDocumentItemDto
                {
                    ProductId = product.Id,
                    Quantity = 3,
                    UnitPrice = 80,
                    IsUnitPriceOverridden = true,
                    VatRate = 20,
                    DiscountRate = 0
                }
            }
        });

        updated.Status.ShouldBe(DocumentStatus.Draft);
        updated.Items.Count.ShouldBe(1);
        updated.Items.Single().UnitPrice.ShouldBe(80m);
        updated.GrandTotal.ShouldBe(288m);
    }

    [Fact]
    public async Task Should_Reserve_Document_Numbers_Of_Soft_Deleted_Drafts()
    {
        var customer = await CreatePartnerAsync("Deleted Draft Customer", BusinessPartnerType.Customer);
        var product = await CreateProductAsync("DELETED-DRAFT-001");
        var document = await _documentAppService.CreateAsync(CreateInvoice(customer.Id, product.Id, "DELETED-DRAFT-001", DateTime.UtcNow.Date));

        await _documentAppService.DeleteAsync(document.Id);

        await Should.ThrowAsync<BusinessException>(() =>
            _documentAppService.CreateAsync(CreateInvoice(customer.Id, product.Id, "DELETED-DRAFT-001", DateTime.UtcNow.Date)));
    }

    private static CreateUpdateDocumentDto CreateInvoice(Guid partnerId, Guid productId, string number, DateTime date)
    {
        return new CreateUpdateDocumentDto
        {
            BusinessPartnerId = partnerId,
            DocNumber = number,
            Type = DocumentType.SaleInvoice,
            Currency = "TRY",
            Date = date,
            DueDate = date.AddDays(7),
            Items =
            {
                new CreateUpdateDocumentItemDto
                {
                    ProductId = productId,
                    Quantity = 1,
                    UnitPrice = 100,
                    VatRate = 20,
                    DiscountRate = 0
                }
            }
        };
    }

    private async Task<BusinessPartnerDto> CreatePartnerAsync(string name, BusinessPartnerType type)
    {
        return await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = name,
            Type = type,
            Currency = "TRY"
        });
    }

    private async Task<ProductDto> CreateProductAsync(string sku)
    {
        return await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = sku,
            Sku = sku,
            Barcode = string.Empty,
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = "Test",
            PurchasePrice = 10,
            SalePrice = 20,
            Quantity = 10,
            CriticalStockLevel = 1,
            TrackStock = true,
            Currency = "TRY",
            PurchaseCurrency = "TRY",
            SaleCurrency = "TRY"
        });
    }
}
