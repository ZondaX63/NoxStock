using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.BusinessPartners;
using NoxStock.Documents;
using NoxStock.ExchangeRates;
using NoxStock.Products;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace NoxStock.EntityFrameworkCore.Applications;

public class ProductCostProfileAppServiceTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly IProductAppService _productAppService;
    private readonly IProductPricingAppService _productPricingAppService;
    private readonly IBusinessPartnerAppService _businessPartnerAppService;
    private readonly IDocumentAppService _documentAppService;
    private readonly IDocumentWorkflowAppService _documentWorkflowAppService;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<ProductCostHistory, Guid> _historyRepository;
    private readonly IRepository<ExchangeRate, Guid> _exchangeRateRepository;

    public ProductCostProfileAppServiceTests()
    {
        _productAppService = ServiceProvider.GetRequiredService<IProductAppService>();
        _productPricingAppService = ServiceProvider.GetRequiredService<IProductPricingAppService>();
        _businessPartnerAppService = ServiceProvider.GetRequiredService<IBusinessPartnerAppService>();
        _documentAppService = ServiceProvider.GetRequiredService<IDocumentAppService>();
        _documentWorkflowAppService = ServiceProvider.GetRequiredService<IDocumentWorkflowAppService>();
        _productRepository = ServiceProvider.GetRequiredService<IRepository<Product, Guid>>();
        _historyRepository = ServiceProvider.GetRequiredService<IRepository<ProductCostHistory, Guid>>();
        _exchangeRateRepository = ServiceProvider.GetRequiredService<IRepository<ExchangeRate, Guid>>();
    }

    [Fact]
    public async Task Should_Save_Try_Purchase_Cost_As_Pricing_Currency_Without_Vat()
    {
        await SeedRatesAsync(usd: 40m, eur: 50m);
        var product = await CreateDynamicProductAsync("COST-TRY-001", "USD");
        var supplier = await CreateSupplierAsync();
        var purchase = await CreatePurchaseAsync(supplier.Id, product.Id, "COST-TRY-INV-001", "TRY", 1000m, 10m, 20m);

        await _documentWorkflowAppService.ApproveAsync(purchase.Id, new ApproveDocumentInputDto());

        var savedProduct = await _productRepository.GetAsync(product.Id);
        var history = (await _historyRepository.GetListAsync(x => x.ProductId == product.Id)).Single();
        savedProduct.LastPurchaseCostCurrencyCode.ShouldBe("USD");
        savedProduct.LastPurchaseCostCurrencyAmount.ShouldBe(22.5m);
        savedProduct.LastPurchaseCostTryAmount.ShouldBe(900m);
        savedProduct.LastPurchaseExchangeRate.ShouldBe(40m);
        savedProduct.AverageInventoryCostTry.ShouldBe(900m);
        savedProduct.SalePrice.ShouldBe(1170m);
        history.SourceNetUnitCost.ShouldBe(900m);
        history.TryEquivalentNetUnitCost.ShouldBe(900m);
        history.ReferenceCurrencyEquivalentCost.ShouldBe(22.5m);
        history.ChangeType.ShouldBe(ProductCostHistoryChangeType.PurchaseApproved);

        var quote = await _productPricingAppService.GetQuoteAsync(new ProductPriceQuoteInputDto
        {
            ProductId = product.Id
        });
        quote.ReferenceCurrencyCode.ShouldBe("USD");
        quote.LastPurchaseCostCurrencyAmount.ShouldBe(22.5m);
        quote.LastPurchaseSourceCurrencyCode.ShouldBe("TRY");
        quote.LastPurchaseSourceNetUnitCost.ShouldBe(900m);
    }

    [Fact]
    public async Task Should_Use_Document_Snapshot_For_Cross_Currency_Purchase_Cost()
    {
        await SeedRatesAsync(usd: 32m, eur: 40m);
        var product = await CreateDynamicProductAsync("COST-EUR-001", "USD");
        var supplier = await CreateSupplierAsync();
        var purchase = await CreatePurchaseAsync(supplier.Id, product.Id, "COST-EUR-INV-001", "EUR", 50m, 0m, 20m);

        await _documentWorkflowAppService.ApproveAsync(purchase.Id, new ApproveDocumentInputDto());

        var history = (await _historyRepository.GetListAsync(x => x.ProductId == product.Id)).Single();
        history.SourceCurrencyCode.ShouldBe("EUR");
        history.SourceExchangeRate.ShouldBe(40m);
        history.TryEquivalentNetUnitCost.ShouldBe(2000m);
        history.ReferenceCurrencyCode.ShouldBe("USD");
        history.ReferenceCurrencyEquivalentCost.ShouldBe(62.5m);
    }

    [Fact]
    public async Task Should_Snapshot_And_Apply_Four_Level_Purchase_Discounts()
    {
        await SeedRatesAsync(usd: 40m, eur: 50m);
        var product = await CreateDynamicProductAsync("COST-DISCOUNT-CHAIN-001", "TRY");
        var supplier = await CreateSupplierAsync();
        var purchase = await _documentAppService.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = supplier.Id,
            DocNumber = "COST-DISCOUNT-CHAIN-INV-001",
            Type = DocumentType.PurchaseInvoice,
            Currency = "TRY",
            Date = DateTime.UtcNow,
            Items =
            {
                new CreateUpdateDocumentItemDto
                {
                    ProductId = product.Id,
                    Quantity = 1m,
                    UnitPrice = 100m,
                    PurchaseDiscount1 = 10m,
                    PurchaseDiscount2 = 5m,
                    VatRate = 20m
                }
            }
        });

        await _documentWorkflowAppService.ApproveAsync(purchase.Id, new ApproveDocumentInputDto());

        var history = (await _historyRepository.GetListAsync(x => x.ProductId == product.Id)).Single();
        history.SourceNetUnitCost.ShouldBe(85.5m);
        var approvedDocument = await _documentAppService.GetAsync(purchase.Id);
        approvedDocument.Items.Single().PurchaseDiscount1.ShouldBe(10m);
        approvedDocument.Items.Single().PurchaseDiscount2.ShouldBe(5m);
    }

    [Fact]
    public async Task Should_Preserve_Draft_Date_Rate_For_Purchase_Cost_Snapshot()
    {
        await SeedRatesAsync(usd: 40m, eur: 50m);
        var product = await CreateDynamicProductAsync("COST-APPROVAL-RATE-001", "USD");
        var supplier = await CreateSupplierAsync();
        var purchase = await CreatePurchaseAsync(supplier.Id, product.Id, "COST-APPROVAL-RATE-INV-001", "TRY", 1000m, 0m, 0m);

        await WithUnitOfWorkAsync(async () =>
        {
            var usd = await _exchangeRateRepository.FirstOrDefaultAsync(x => x.Currency == "USD");
            usd!.Rate = 50m;
            usd.RateDate = DateTime.UtcNow.AddDays(1);
            await _exchangeRateRepository.UpdateAsync(usd);
        });

        await _documentWorkflowAppService.ApproveAsync(purchase.Id, new ApproveDocumentInputDto());

        var history = (await _historyRepository.GetListAsync(x => x.ProductId == product.Id)).Single();
        history.TryEquivalentNetUnitCost.ShouldBe(1000m);
        history.ReferenceCurrencyEquivalentCost.ShouldBe(25m);
        (await _productRepository.GetAsync(product.Id)).LastPurchaseExchangeRate.ShouldBe(40m);
    }

    [Fact]
    public async Task Should_Not_Update_Cost_Profile_For_Draft_And_Create_History_Only_Once()
    {
        await SeedRatesAsync(usd: 40m, eur: 50m);
        var product = await CreateDynamicProductAsync("COST-DRAFT-001", "USD");
        var supplier = await CreateSupplierAsync();
        var purchase = await CreatePurchaseAsync(supplier.Id, product.Id, "COST-DRAFT-INV-001", "USD", 25m, 0m, 0m);

        (await _productRepository.GetAsync(product.Id)).LastPurchaseCostCurrencyAmount.ShouldBeNull();
        (await _historyRepository.GetListAsync(x => x.ProductId == product.Id)).ShouldBeEmpty();

        await _documentWorkflowAppService.ApproveAsync(purchase.Id, new ApproveDocumentInputDto());

        (await _historyRepository.GetListAsync(x => x.ProductId == product.Id)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Restore_Previous_Active_Cost_After_Cancelling_Latest_Purchase()
    {
        await SeedRatesAsync(usd: 40m, eur: 50m);
        var product = await CreateDynamicProductAsync("COST-CANCEL-001", "USD");
        var supplier = await CreateSupplierAsync();
        var first = await CreatePurchaseAsync(supplier.Id, product.Id, "COST-CANCEL-001", "USD", 10m, 0m, 0m);
        await _documentWorkflowAppService.ApproveAsync(first.Id, new ApproveDocumentInputDto());
        var second = await CreatePurchaseAsync(supplier.Id, product.Id, "COST-CANCEL-002", "USD", 20m, 0m, 0m);
        await _documentWorkflowAppService.ApproveAsync(second.Id, new ApproveDocumentInputDto());

        (await _productRepository.GetAsync(product.Id)).LastPurchaseCostCurrencyAmount.ShouldBe(20m);
        await _documentWorkflowAppService.CancelAsync(second.Id);

        var restoredProduct = await _productRepository.GetAsync(product.Id);
        var histories = await _historyRepository.GetListAsync(x => x.ProductId == product.Id);
        restoredProduct.LastPurchaseCostCurrencyAmount.ShouldBe(10m);
        histories.Count(x => x.ChangeType == ProductCostHistoryChangeType.PurchaseApproved).ShouldBe(2);
        histories.Count(x => x.ChangeType == ProductCostHistoryChangeType.PurchaseCancelled).ShouldBe(1);
    }

    [Fact]
    public async Task Should_Keep_Weighted_Try_Cost_And_Automatic_Price_When_Currency_Rate_Changes()
    {
        await SeedRatesAsync(usd: 40m, eur: 50m);
        var product = await CreateDynamicProductAsync("COST-LIVE-001", "USD", markup: 30m);
        var supplier = await CreateSupplierAsync();
        var purchase = await CreatePurchaseAsync(supplier.Id, product.Id, "COST-LIVE-INV-001", "USD", 25m, 0m, 0m);
        await _documentWorkflowAppService.ApproveAsync(purchase.Id, new ApproveDocumentInputDto());

        var beforeRateChange = await _productPricingAppService.GetQuoteAsync(new ProductPriceQuoteInputDto { ProductId = product.Id });
        await WithUnitOfWorkAsync(async () =>
        {
            var usd = await _exchangeRateRepository.FirstOrDefaultAsync(x => x.Currency == "USD");
            usd!.Rate = 42m;
            await _exchangeRateRepository.UpdateAsync(usd);
        });
        var afterRateChange = await _productPricingAppService.GetQuoteAsync(new ProductPriceQuoteInputDto { ProductId = product.Id });

        beforeRateChange.UnitPrice.ShouldBe(1300m);
        afterRateChange.UnitPrice.ShouldBe(1300m);
        afterRateChange.LastPurchaseCostCurrencyAmount.ShouldBe(25m);
        afterRateChange.ProductCostHistoryId.ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_Protect_Try_Purchase_Cost_In_Reference_Currency_And_Quote_Try()
    {
        await SeedRatesAsync(usd: 40m, eur: 50m);
        var product = await CreateDynamicProductAsync("COST-PROTECTED-001", "USD", markup: 20m);
        var supplier = await CreateSupplierAsync();
        var purchase = await CreatePurchaseAsync(supplier.Id, product.Id, "COST-PROTECTED-PURCHASE-001", "TRY", 1000m, 0m, 20m);
        await _documentWorkflowAppService.ApproveAsync(purchase.Id, new ApproveDocumentInputDto());

        var storedProduct = await _productRepository.GetAsync(product.Id);
        storedProduct.LastPurchaseCostTryAmount.ShouldBe(1000m);
        storedProduct.LastPurchaseCostCurrencyCode.ShouldBe("USD");
        storedProduct.LastPurchaseCostCurrencyAmount.ShouldBe(25m);
        storedProduct.LastPurchaseExchangeRate.ShouldBe(40m);

        await WithUnitOfWorkAsync(async () =>
        {
            var usd = await _exchangeRateRepository.FirstOrDefaultAsync(x => x.Currency == "USD");
            usd!.Rate = 41m;
            await _exchangeRateRepository.UpdateAsync(usd);
        });

        var usdQuote = await _productPricingAppService.GetQuoteAsync(new ProductPriceQuoteInputDto { ProductId = product.Id });
        var tryQuote = await _productPricingAppService.GetQuoteAsync(new ProductPriceQuoteInputDto { ProductId = product.Id });
        var eurQuote = await _productPricingAppService.GetQuoteAsync(new ProductPriceQuoteInputDto { ProductId = product.Id });

        usdQuote.ReferenceSaleUnitPrice.ShouldBe(29.27m);
        usdQuote.UnitPrice.ShouldBe(1200m);
        tryQuote.UnitPrice.ShouldBe(1200m);
        eurQuote.UnitPrice.ShouldBe(1200m);
        tryQuote.LastPurchaseCostTryAmount.ShouldBe(1000m);
        tryQuote.LastPurchaseCostCurrencyAmount.ShouldBe(25m);
    }

    [Fact]
    public async Task Should_Lock_Dynamic_Sales_Line_Price_And_Cost_Snapshot()
    {
        await SeedRatesAsync(usd: 40m, eur: 50m);
        var product = await CreateDynamicProductAsync("COST-SALE-SNAPSHOT-001", "USD", markup: 30m);
        var supplier = await CreateSupplierAsync();
        var purchase = await CreatePurchaseAsync(supplier.Id, product.Id, "COST-SALE-PURCHASE-001", "USD", 25m, 0m, 0m);
        await _documentWorkflowAppService.ApproveAsync(purchase.Id, new ApproveDocumentInputDto());
        var customer = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = "Cost Snapshot Customer",
            Type = BusinessPartnerType.Customer
        });

        var sale = await _documentAppService.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = customer.Id,
            DocNumber = "COST-SALE-001",
            Type = DocumentType.SaleInvoice,
            Currency = "TRY",
            Date = DateTime.UtcNow,
            Items =
            {
                new CreateUpdateDocumentItemDto
                {
                    ProductId = product.Id,
                    Quantity = 1m,
                    UnitPrice = 0m,
                    VatRate = 0m,
                    DiscountRate = 0m
                }
            }
        });

        var item = sale.Items.Single();
        item.UnitPrice.ShouldBe(1300m);
        item.PriceExchangeRate.ShouldBe(1m);
        item.ProductCostHistoryId.ShouldNotBeNull();
        item.IsUnitPriceOverridden.ShouldBeFalse();
        item.PricingMode.ShouldBe(ProductPricingMode.DynamicCurrencyMarkup);

        await WithUnitOfWorkAsync(async () =>
        {
            var usd = await _exchangeRateRepository.FirstOrDefaultAsync(x => x.Currency == "USD");
            usd!.Rate = 42m;
            await _exchangeRateRepository.UpdateAsync(usd);
        });

        (await _documentAppService.GetAsync(sale.Id)).Items.Single().UnitPrice.ShouldBe(1300m);

        var approved = await _documentWorkflowAppService.ApproveAsync(sale.Id, new ApproveDocumentInputDto());
        var approvedItem = approved.Items.Single();
        approvedItem.UnitPrice.ShouldBe(1300m);
        approvedItem.UnitPriceTry.ShouldBe(1300m);
        approvedItem.CostCurrencyCode.ShouldBe("USD");
        approvedItem.CostCurrencyAmount.ShouldBe(25m);
        approvedItem.CostTryAmount.ShouldBe(1000m);
        approvedItem.AppliedMarkupPercent.ShouldBe(30m);
        approvedItem.ReferenceCurrencyCode.ShouldBe("USD");
        approvedItem.ReferenceUnitPrice.ShouldBe(30.95m);
        approvedItem.ReferenceExchangeRate.ShouldBe(42m);
        approvedItem.PricingSnapshotDate.ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_Use_Manual_Card_Cost_Until_Approved_Inventory_Cost_Exists()
    {
        await SeedRatesAsync(usd: 40m, eur: 50m);
        var product = await CreateDynamicProductAsync("COST-MISSING-001", "USD", markup: 20m);

        var quote = await _productPricingAppService.GetQuoteAsync(new ProductPriceQuoteInputDto
        {
            ProductId = product.Id
        });

        quote.IsAvailable.ShouldBeTrue();
        quote.UnitPrice.ShouldBe(48m);
        quote.AverageInventoryCostTry.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Recalculate_Weighted_Average_Inventory_Cost_And_Automatic_Price()
    {
        await SeedRatesAsync(usd: 40m, eur: 50m);
        var productDto = await CreateDynamicProductAsync("WEIGHTED-COST-001", "TRY", markup: 30m);
        await WithUnitOfWorkAsync(async () =>
        {
            var product = await _productRepository.GetAsync(productDto.Id);
            product.Quantity = 5m;
            product.AverageInventoryCostTry = 800m;
            await _productRepository.UpdateAsync(product);
        });
        var supplier = await CreateSupplierAsync();
        var purchase = await _documentAppService.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = supplier.Id,
            DocNumber = "WEIGHTED-COST-INV-001",
            Type = DocumentType.PurchaseInvoice,
            Currency = "TRY",
            Date = DateTime.UtcNow,
            Items =
            {
                new CreateUpdateDocumentItemDto
                {
                    ProductId = productDto.Id,
                    Quantity = 10m,
                    UnitPrice = 890m,
                    VatRate = 20m
                }
            }
        });

        var approved = await _documentWorkflowAppService.ApproveAsync(purchase.Id, new ApproveDocumentInputDto());
        var saved = await _productRepository.GetAsync(productDto.Id);

        saved.Quantity.ShouldBe(15m);
        saved.AverageInventoryCostTry.ShouldBe(860m);
        saved.LastPurchaseCostTryAmount.ShouldBe(890m);
        saved.SalePrice.ShouldBe(1118m);
        approved.Items.Single().CostTryAmount.ShouldBe(890m);
        approved.Items.Single().TotalCostTryAmount.ShouldBe(8900m);
    }

    private async Task SeedRatesAsync(decimal usd, decimal eur)
    {
        await _exchangeRateRepository.InsertAsync(new ExchangeRate(Guid.NewGuid(), "USD", usd, DateTime.UtcNow));
        await _exchangeRateRepository.InsertAsync(new ExchangeRate(Guid.NewGuid(), "EUR", eur, DateTime.UtcNow));
    }

    private Task<ProductDto> CreateDynamicProductAsync(string sku, string pricingCurrency, decimal markup = 30m)
    {
        return _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = sku,
            Sku = sku,
            Barcode = string.Empty,
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = "C-01",
            PurchasePrice = 1m,
            PurchaseCurrency = pricingCurrency,
            ExchangeRateAtPurchase = pricingCurrency == "USD" ? 40m : pricingCurrency == "EUR" ? 50m : 1m,
            SalePrice = 1m,
            PricingMode = ProductPricingMode.DynamicCurrencyMarkup,
            ProfitMarkupPercent = markup,
            UseCurrentExchangeRateForSale = true,
            Quantity = 0m,
            CriticalStockLevel = 1m,
            TrackStock = true
        });
    }

    private Task<BusinessPartnerDto> CreateSupplierAsync()
    {
        return _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = "Cost Profile Supplier " + Guid.NewGuid().ToString("N")[..8],
            Type = BusinessPartnerType.Supplier
        });
    }

    private Task<DocumentDto> CreatePurchaseAsync(Guid supplierId, Guid productId, string number, string currency, decimal unitPrice, decimal discountRate, decimal vatRate)
    {
        return _documentAppService.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = supplierId,
            DocNumber = number,
            Type = DocumentType.PurchaseInvoice,
            Currency = currency,
            Date = DateTime.UtcNow,
            Items =
            {
                new CreateUpdateDocumentItemDto
                {
                    ProductId = productId,
                    Quantity = 1m,
                    UnitPrice = unitPrice,
                    DiscountRate = discountRate,
                    VatRate = vatRate
                }
            }
        });
    }
}
