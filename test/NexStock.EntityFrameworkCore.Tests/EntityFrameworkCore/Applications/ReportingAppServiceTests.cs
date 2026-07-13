using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NexStock.Accounts;
using NexStock.BusinessPartners;
using NexStock.Documents;
using NexStock.Products;
using NexStock.Reports;
using NexStock.StockMovements;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace NexStock.EntityFrameworkCore.Applications;

public class ReportingAppServiceTests : NexStockEntityFrameworkCoreTestBase
{
    private readonly IReportingAppService _reportingAppService;
    private readonly IProductAppService _productAppService;
    private readonly IBusinessPartnerAppService _businessPartnerAppService;
    private readonly IDocumentAppService _documentAppService;
    private readonly IAccountAppService _accountAppService;
    private readonly IRepository<AccountTransaction, Guid> _accountTransactionRepository;
    private readonly IRepository<StockMovement, Guid> _stockMovementRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;

    public ReportingAppServiceTests()
    {
        _reportingAppService = ServiceProvider.GetRequiredService<IReportingAppService>();
        _productAppService = ServiceProvider.GetRequiredService<IProductAppService>();
        _businessPartnerAppService = ServiceProvider.GetRequiredService<IBusinessPartnerAppService>();
        _documentAppService = ServiceProvider.GetRequiredService<IDocumentAppService>();
        _accountAppService = ServiceProvider.GetRequiredService<IAccountAppService>();
        _accountTransactionRepository = ServiceProvider.GetRequiredService<IRepository<AccountTransaction, Guid>>();
        _stockMovementRepository = ServiceProvider.GetRequiredService<IRepository<StockMovement, Guid>>();
        _businessPartnerRepository = ServiceProvider.GetRequiredService<IRepository<BusinessPartner, Guid>>();
    }

    [Fact]
    public async Task Should_Build_Operational_Report_From_Commercial_Ledger()
    {
        var product = await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = "Reporting Product",
            Sku = "RPT-001",
            Barcode = "869000000101",
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = "R1",
            PurchasePrice = 40,
            SalePrice = 100,
            Quantity = 5,
            CriticalStockLevel = 5,
            TrackStock = true,
            Currency = "TRY",
            PurchaseCurrency = "TRY",
            SaleCurrency = "TRY"
        });
        var customer = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = "Reporting Customer",
            Type = BusinessPartnerType.Customer,
            Currency = "TRY"
        });
        var supplier = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = "Reporting Supplier",
            Type = BusinessPartnerType.Supplier,
            Currency = "TRY"
        });
        var customerEntity = await _businessPartnerRepository.GetAsync(customer.Id);
        customerEntity.Balance = 120;
        await _businessPartnerRepository.UpdateAsync(customerEntity);
        var supplierEntity = await _businessPartnerRepository.GetAsync(supplier.Id);
        supplierEntity.Balance = -60;
        await _businessPartnerRepository.UpdateAsync(supplierEntity);

        var today = DateTime.UtcNow.Date;
        await CreateDocumentAsync(customer.Id, product.Id, "RPT-SALE", DocumentType.SaleInvoice, 100, today.AddDays(-10));
        await CreateDocumentAsync(supplier.Id, product.Id, "RPT-PURCHASE", DocumentType.PurchaseInvoice, 50, today.AddDays(-5));

        var account = await _accountAppService.CreateAsync(new CreateUpdateAccountDto
        {
            Name = "Reporting Cash",
            Type = AccountType.Cash,
            Currency = "TRY",
            Balance = 20,
            IsActive = true
        });
        await _accountTransactionRepository.InsertAsync(new AccountTransaction(
            Guid.NewGuid(), NexStockTestConsts.TenantId, account.Id, AccountTransactionType.Income, 30, "TRY", today));
        await _accountTransactionRepository.InsertAsync(new AccountTransaction(
            Guid.NewGuid(), NexStockTestConsts.TenantId, account.Id, AccountTransactionType.Expense, 10, "TRY", today));
        await _stockMovementRepository.InsertAsync(new StockMovement(
            Guid.NewGuid(), NexStockTestConsts.TenantId, product.Id, StockMovementType.ManualOut, -1, 5, today, description: "Reporting movement"));

        var report = await _reportingAppService.GetSnapshotAsync(new ReportRequestDto
        {
            DateFrom = today.AddDays(-30),
            DateTo = today
        });

        report.Summary.Sales.ShouldBe(120);
        report.Summary.Purchases.ShouldBe(60);
        report.Summary.CashIn.ShouldBe(30);
        report.Summary.CashOut.ShouldBe(10);
        report.Summary.NetCashFlow.ShouldBe(20);
        report.Summary.Income.ShouldBe(30);
        report.Summary.Expense.ShouldBe(10);
        report.Summary.Receivables.ShouldBe(120);
        report.Summary.Payables.ShouldBe(60);
        report.Summary.CriticalStockCount.ShouldBe(1);
        report.VatRates.Single(x => x.VatRate == 20).SalesVat.ShouldBe(20);
        report.VatRates.Single(x => x.VatRate == 20).PurchaseVat.ShouldBe(10);
        report.Aging.Single(x => x.Bucket == "0-30 gün").Receivables.ShouldBe(120);
        report.ProductProfitability.Single(x => x.ProductId == product.Id).GrossProfitEstimate.ShouldBe(80);
        report.StockMovements.Single(x => x.ProductName == product.Name).Quantity.ShouldBe(-1);
    }

    private async Task CreateDocumentAsync(Guid partnerId, Guid productId, string number, DocumentType type, decimal unitPrice, DateTime dueDate)
    {
        await _documentAppService.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = partnerId,
            DocNumber = number,
            Type = type,
            Status = DocumentStatus.Approved,
            Currency = "TRY",
            ExchangeRate = 1,
            Date = dueDate.AddDays(-5),
            DueDate = dueDate,
            Items =
            {
                new CreateUpdateDocumentItemDto
                {
                    ProductId = productId,
                    Quantity = 1,
                    UnitPrice = unitPrice,
                    VatRate = 20,
                    DiscountRate = 0
                }
            }
        });
    }
}
