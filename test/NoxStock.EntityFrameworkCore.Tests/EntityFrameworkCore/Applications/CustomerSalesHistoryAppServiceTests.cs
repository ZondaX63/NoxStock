using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.EntityFrameworkCore;
using NoxStock.Accounts;
using NoxStock.Brands;
using NoxStock.BusinessPartners;
using NoxStock.Categories;
using NoxStock.CustomerSalesHistory;
using NoxStock.Documents;
using NoxStock.Products;
using NoxStock.Sales;
using Shouldly;
using Xunit;

namespace NoxStock.EntityFrameworkCore.Applications;

public class CustomerSalesHistoryAppServiceTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly ICustomerSalesHistoryAppService _salesHistoryAppService;
    private readonly IBusinessPartnerAppService _businessPartnerAppService;
    private readonly IProductAppService _productAppService;
    private readonly IBrandAppService _brandAppService;
    private readonly ICategoryAppService _categoryAppService;
    private readonly IDocumentAppService _documentAppService;
    private readonly IDocumentWorkflowAppService _documentWorkflowAppService;
    private readonly IPosSaleAppService _posSaleAppService;
    private readonly IAccountAppService _accountAppService;

    public CustomerSalesHistoryAppServiceTests()
    {
        _salesHistoryAppService = ServiceProvider.GetRequiredService<ICustomerSalesHistoryAppService>();
        _businessPartnerAppService = ServiceProvider.GetRequiredService<IBusinessPartnerAppService>();
        _productAppService = ServiceProvider.GetRequiredService<IProductAppService>();
        _brandAppService = ServiceProvider.GetRequiredService<IBrandAppService>();
        _categoryAppService = ServiceProvider.GetRequiredService<ICategoryAppService>();
        _documentAppService = ServiceProvider.GetRequiredService<IDocumentAppService>();
        _documentWorkflowAppService = ServiceProvider.GetRequiredService<IDocumentWorkflowAppService>();
        _posSaleAppService = ServiceProvider.GetRequiredService<IPosSaleAppService>();
        _accountAppService = ServiceProvider.GetRequiredService<IAccountAppService>();
    }

    [Fact]
    public async Task Should_Return_Approved_Invoice_And_Pos_Lines_With_Consistent_Summary()
    {
        var fixture = await CreateFixtureAsync();
        await CreateApprovedInvoiceAsync(fixture.Customer.Id, fixture.Product.Id, "HISTORY-INV-001", DateTime.UtcNow.AddDays(-2), 2, 100);
        await CreateDraftInvoiceAsync(fixture.Customer.Id, fixture.Product.Id, "HISTORY-DRAFT-001", DateTime.UtcNow.AddDays(-1));

        var account = await _accountAppService.CreateAsync(new CreateUpdateAccountDto
        {
            Name = "History Cash",
            Type = AccountType.Cash,
            Currency = "TRY",
            IsActive = true
        });
        await _posSaleAppService.CreateAsync(new CreatePosSaleDto
        {
            BusinessPartnerId = fixture.Customer.Id,
            PaymentMethod = PosPaymentMethod.Cash,
            AccountId = account.Id,
            Items = { new CreatePosSaleItemDto { ProductId = fixture.Product.Id, Quantity = 1 } }
        });

        var history = await _salesHistoryAppService.GetCustomerSalesHistoryAsync(new CustomerSalesHistoryInput
        {
            CustomerId = fixture.Customer.Id,
            PeriodPreset = CustomerSalesHistoryPeriodPreset.Custom,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow,
            MaxResultCount = 50
        });

        history.TotalCount.ShouldBe(2);
        history.Summary.DocumentCount.ShouldBe(2);
        history.Summary.TotalQuantity.ShouldBe(3);
        history.Summary.NetSales.Single(x => x.Currency == "TRY").Amount.ShouldBe(250);
        history.Summary.TopProductName.ShouldBe(fixture.Product.Name);
        history.Groups.SelectMany(group => group.Rows).Select(row => row.DocumentNumber)
            .ShouldContain("HISTORY-INV-001");
        history.Groups.SelectMany(group => group.Rows).Select(row => row.DocumentNumber)
            .ShouldNotContain("HISTORY-DRAFT-001");
        history.Groups.SelectMany(group => group.Rows).Select(row => row.TransactionType)
            .ShouldContain(CustomerSalesHistoryDocumentType.PosSale);
    }

    [Fact]
    public async Task Should_Apply_Product_Brand_Category_Document_And_Currency_Filters_Without_Duplicating_Pages()
    {
        var fixture = await CreateFixtureAsync();
        var otherProduct = await CreateProductAsync("HISTORY-OTHER", null, null);
        await CreateApprovedInvoiceAsync(fixture.Customer.Id, fixture.Product.Id, "HISTORY-FILTER-ONE", DateTime.UtcNow.AddDays(-3), 1, 60);
        await CreateApprovedInvoiceAsync(fixture.Customer.Id, otherProduct.Id, "HISTORY-FILTER-TWO", DateTime.UtcNow.AddDays(-2), 1, 70);
        await CreateApprovedInvoiceAsync(fixture.Customer.Id, fixture.Product.Id, "HISTORY-FILTER-THREE", DateTime.UtcNow.AddDays(-1), 1, 80);

        var filtered = await _salesHistoryAppService.GetCustomerSalesHistoryAsync(new CustomerSalesHistoryInput
        {
            CustomerId = fixture.Customer.Id,
            PeriodPreset = CustomerSalesHistoryPeriodPreset.Custom,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow,
            ProductSearch = "History Product",
            DocumentNumber = "FILTER",
            BrandId = fixture.Brand.Id,
            CategoryId = fixture.Category.Id,
            Currency = "TRY",
            DocumentType = CustomerSalesHistoryDocumentType.SaleInvoice,
            MaxResultCount = 1
        });

        filtered.TotalCount.ShouldBe(2);
        filtered.Groups.SelectMany(group => group.Rows).Single().DocumentNumber.ShouldBe("HISTORY-FILTER-THREE");
        filtered.HasNextPage.ShouldBeTrue();

        var nextPage = await _salesHistoryAppService.GetCustomerSalesHistoryAsync(new CustomerSalesHistoryInput
        {
            CustomerId = fixture.Customer.Id,
            PeriodPreset = CustomerSalesHistoryPeriodPreset.Custom,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow,
            ProductSearch = "History Product",
            BrandId = fixture.Brand.Id,
            CategoryId = fixture.Category.Id,
            SkipCount = 1,
            MaxResultCount = 1
        });

        nextPage.Groups.SelectMany(group => group.Rows).Single().DocumentNumber.ShouldBe("HISTORY-FILTER-ONE");
        nextPage.Groups.SelectMany(group => group.Rows).Single().DocumentItemId
            .ShouldNotBe(filtered.Groups.SelectMany(group => group.Rows).Single().DocumentItemId);
    }

    [Fact]
    public async Task Should_Exclude_Cancelled_Sales_And_Return_An_Empty_Result_For_Return_Filter_Until_Return_Documents_Exist()
    {
        var fixture = await CreateFixtureAsync();
        await CreateApprovedInvoiceAsync(fixture.Customer.Id, fixture.Product.Id, "HISTORY-VALID", DateTime.UtcNow.AddDays(-2), 1, 100);

        var account = await _accountAppService.CreateAsync(new CreateUpdateAccountDto
        {
            Name = "History Cancel Cash",
            Type = AccountType.Cash,
            Currency = "TRY",
            IsActive = true
        });
        var cancelledSale = await _posSaleAppService.CreateAsync(new CreatePosSaleDto
        {
            BusinessPartnerId = fixture.Customer.Id,
            PaymentMethod = PosPaymentMethod.Cash,
            AccountId = account.Id,
            Items = { new CreatePosSaleItemDto { ProductId = fixture.Product.Id, Quantity = 1 } }
        });
        await _posSaleAppService.CancelAsync(cancelledSale.Id);

        var sales = await _salesHistoryAppService.GetCustomerSalesHistoryAsync(CreateCustomPeriodInput(fixture.Customer.Id));
        sales.TotalCount.ShouldBe(1);
        sales.Groups.SelectMany(group => group.Rows).Single().DocumentNumber.ShouldBe("HISTORY-VALID");

        var returns = await _salesHistoryAppService.GetCustomerSalesHistoryAsync(new CustomerSalesHistoryInput
        {
            CustomerId = fixture.Customer.Id,
            PeriodPreset = CustomerSalesHistoryPeriodPreset.Custom,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow,
            OnlyReturns = true
        });
        returns.TotalCount.ShouldBe(0);
        returns.Summary.NetSales.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Keep_Query_Count_Bounded_For_A_Large_Result_Set()
    {
        var fixture = await CreateFixtureAsync();
        for (var index = 0; index < 110; index++)
        {
            await CreateApprovedInvoiceAsync(
                fixture.Customer.Id,
                fixture.Product.Id,
                $"HISTORY-BOUNDED-{index:D3}",
                DateTime.UtcNow.AddDays(-index % 30),
                1,
                10);
        }

        SqlCommandCounter.Instance.Reset();
        var history = await _salesHistoryAppService.GetCustomerSalesHistoryAsync(new CustomerSalesHistoryInput
        {
            CustomerId = fixture.Customer.Id,
            PeriodPreset = CustomerSalesHistoryPeriodPreset.Custom,
            StartDate = DateTime.UtcNow.AddDays(-31),
            EndDate = DateTime.UtcNow,
            MaxResultCount = 25
        });

        history.TotalCount.ShouldBe(110);
        history.Groups.SelectMany(group => group.Rows).Count().ShouldBe(25);
        SqlCommandCounter.Instance.CommandCount.ShouldBeLessThanOrEqualTo(16);
    }

    private CustomerSalesHistoryInput CreateCustomPeriodInput(Guid customerId)
    {
        return new CustomerSalesHistoryInput
        {
            CustomerId = customerId,
            PeriodPreset = CustomerSalesHistoryPeriodPreset.Custom,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow,
            MaxResultCount = 50
        };
    }

    private async Task<(BusinessPartnerDto Customer, ProductDto Product, BrandDto Brand, CategoryDto Category)> CreateFixtureAsync()
    {
        var brand = await _brandAppService.CreateAsync(new CreateUpdateBrandDto { Name = "History Brand" });
        var category = await _categoryAppService.CreateAsync(new CreateUpdateCategoryDto { Name = "History Category" });
        var customer = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = "History Customer",
            Type = BusinessPartnerType.Customer,
            Currency = "TRY",
            CreditLimit = 1000
        });
        var product = await CreateProductAsync("HISTORY-PRIMARY", brand.Id, category.Id);
        return (customer, product, brand, category);
    }

    private Task<ProductDto> CreateProductAsync(string sku, Guid? brandId, Guid? categoryId)
    {
        return _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = sku == "HISTORY-PRIMARY" ? "History Product" : "Other Product",
            Sku = sku,
            Barcode = string.Empty,
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = "History",
            PurchasePrice = 10,
            SalePrice = 50,
            Quantity = 100,
            CriticalStockLevel = 1,
            TrackStock = true,
            Currency = "TRY",
            PurchaseCurrency = "TRY",
            SaleCurrency = "TRY",
            BrandId = brandId,
            CategoryId = categoryId
        });
    }

    private async Task CreateApprovedInvoiceAsync(Guid customerId, Guid productId, string documentNumber, DateTime date, decimal quantity, decimal unitPrice)
    {
        var invoice = await _documentAppService.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = customerId,
            DocNumber = documentNumber,
            Type = DocumentType.SaleInvoice,
            Currency = "TRY",
            Date = date,
            DueDate = date.AddDays(7),
            Items =
            {
                new CreateUpdateDocumentItemDto
                {
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    VatRate = 0,
                    DiscountRate = 0
                }
            }
        });
        await _documentWorkflowAppService.ApproveAsync(invoice.Id, new ApproveDocumentInputDto());
    }

    private Task CreateDraftInvoiceAsync(Guid customerId, Guid productId, string documentNumber, DateTime date)
    {
        return _documentAppService.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = customerId,
            DocNumber = documentNumber,
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
                    UnitPrice = 10,
                    VatRate = 0,
                    DiscountRate = 0
                }
            }
        });
    }
}
