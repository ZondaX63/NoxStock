using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NexStock.BusinessPartners;
using NexStock.Dashboard;
using NexStock.Documents;
using NexStock.Products;
using Shouldly;
using Xunit;

namespace NexStock.EntityFrameworkCore.Applications;

public class DashboardAppServiceTests : NexStockEntityFrameworkCoreTestBase
{
    private readonly IDashboardAppService _dashboardAppService;
    private readonly IBusinessPartnerAppService _businessPartnerAppService;
    private readonly IDocumentAppService _documentAppService;
    private readonly IProductAppService _productAppService;

    public DashboardAppServiceTests()
    {
        _dashboardAppService = ServiceProvider.GetRequiredService<IDashboardAppService>();
        _businessPartnerAppService = ServiceProvider.GetRequiredService<IBusinessPartnerAppService>();
        _documentAppService = ServiceProvider.GetRequiredService<IDocumentAppService>();
        _productAppService = ServiceProvider.GetRequiredService<IProductAppService>();
    }

    [Fact]
    public async Task Should_List_Only_Open_Overdue_And_Due_Soon_Invoices()
    {
        var partner = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = "Dashboard Due Customer",
            Type = BusinessPartnerType.Customer,
            Currency = "TRY"
        });

        var product = await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = "Dashboard Due Product",
            Sku = "DUE-001",
            Barcode = string.Empty,
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = string.Empty,
            PurchasePrice = 50,
            SalePrice = 100,
            Quantity = 10,
            CriticalStockLevel = 1,
            TrackStock = true,
            Currency = "TRY",
            PurchaseCurrency = "TRY",
            SaleCurrency = "TRY"
        });

        var today = DateTime.UtcNow.Date;
        await CreateInvoiceAsync(partner.Id, product.Id, "DUE-OVERDUE", today.AddDays(-2), DocumentStatus.Approved);
        await CreateInvoiceAsync(partner.Id, product.Id, "DUE-SOON", today.AddDays(4), DocumentStatus.PartiallyPaid);
        await CreateInvoiceAsync(partner.Id, product.Id, "DUE-PAID", today.AddDays(-1), DocumentStatus.Paid);

        var summary = await _dashboardAppService.GetSummaryAsync();

        summary.DueDocuments.Count.ShouldBe(2);
        summary.DueDocuments.Select(x => x.DocNumber).ShouldContain("DUE-OVERDUE");
        summary.DueDocuments.Select(x => x.DocNumber).ShouldContain("DUE-SOON");
        summary.DueDocuments.Select(x => x.DocNumber).ShouldNotContain("DUE-PAID");

        var overdueDocuments = await _documentAppService.GetListAsync(new DocumentGetListInput
        {
            DueFilter = DocumentDueFilter.Overdue,
            MaxResultCount = 10
        });
        var dueSoonDocuments = await _documentAppService.GetListAsync(new DocumentGetListInput
        {
            DueFilter = DocumentDueFilter.DueSoon,
            MaxResultCount = 10
        });

        overdueDocuments.Items.Select(x => x.DocNumber).ShouldContain("DUE-OVERDUE");
        overdueDocuments.Items.Select(x => x.DocNumber).ShouldNotContain("DUE-SOON");
        dueSoonDocuments.Items.Select(x => x.DocNumber).ShouldContain("DUE-SOON");
        dueSoonDocuments.Items.Select(x => x.DocNumber).ShouldNotContain("DUE-PAID");
    }

    private async Task CreateInvoiceAsync(
        Guid partnerId,
        Guid productId,
        string docNumber,
        DateTime dueDate,
        DocumentStatus status)
    {
        await _documentAppService.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = partnerId,
            DocNumber = docNumber,
            Type = DocumentType.SaleInvoice,
            Status = status,
            Currency = "TRY",
            ExchangeRate = 1,
            Date = dueDate.AddDays(-7),
            DueDate = dueDate,
            PaidAmount = status == DocumentStatus.PartiallyPaid ? 25 : status == DocumentStatus.Paid ? 100 : 0,
            Items =
            {
                new CreateUpdateDocumentItemDto
                {
                    ProductId = productId,
                    Quantity = 1,
                    UnitPrice = 100,
                    VatRate = 0,
                    DiscountRate = 0
                }
            }
        });
    }
}
