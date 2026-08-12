using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.BusinessPartners;
using NoxStock.Documents;
using NoxStock.ExchangeRates;
using NoxStock.Products;
using NoxStock.PrintLabels;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace NoxStock.EntityFrameworkCore.Applications;

public class ProductPricingAppServiceTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly IProductAppService _productAppService;
    private readonly IProductPricingAppService _productPricingAppService;
    private readonly IBusinessPartnerAppService _businessPartnerAppService;
    private readonly IDocumentAppService _documentAppService;
    private readonly IDocumentWorkflowAppService _documentWorkflowAppService;
    private readonly IRepository<ExchangeRate, Guid> _exchangeRateRepository;
    private readonly ILabelProductLookupAppService _labelProductLookupAppService;
    private readonly IRepository<Product, Guid> _productRepository;

    public ProductPricingAppServiceTests()
    {
        _productAppService = ServiceProvider.GetRequiredService<IProductAppService>();
        _productPricingAppService = ServiceProvider.GetRequiredService<IProductPricingAppService>();
        _businessPartnerAppService = ServiceProvider.GetRequiredService<IBusinessPartnerAppService>();
        _documentAppService = ServiceProvider.GetRequiredService<IDocumentAppService>();
        _documentWorkflowAppService = ServiceProvider.GetRequiredService<IDocumentWorkflowAppService>();
        _exchangeRateRepository = ServiceProvider.GetRequiredService<IRepository<ExchangeRate, Guid>>();
        _labelProductLookupAppService = ServiceProvider.GetRequiredService<ILabelProductLookupAppService>();
        _productRepository = ServiceProvider.GetRequiredService<IRepository<Product, Guid>>();
    }

    [Fact]
    public async Task Should_Include_Reference_Cost_In_Inventory_Summary_When_Approved_Cost_Is_Not_Available()
    {
        var before = await _productAppService.GetInventorySummaryAsync();
        var product = await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = "Reference Cost Summary Product",
            Sku = "REFERENCE-SUMMARY-001",
            Barcode = "869000009901",
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = string.Empty,
            PurchaseListPrice = 1000m,
            PurchaseCurrency = "TRY",
            ExchangeRateAtPurchase = 1m,
            SalePrice = 1500m,
            PricingMode = ProductPricingMode.Manual,
            ProfitMarkupPercent = 50m,
            Quantity = 30m,
            TrackStock = true
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var saved = await _productRepository.GetAsync(product.Id);
            saved.AverageInventoryCostTry = null;
            await _productRepository.UpdateAsync(saved, autoSave: true);
        });

        var after = await _productAppService.GetInventorySummaryAsync();

        (after.TotalInventoryCost - before.TotalInventoryCost).ShouldBe(30000m);
        after.MissingCostCount.ShouldBe(before.MissingCostCount);
    }

    [Fact]
    public async Task Should_Bound_Label_Product_Search()
    {
        for (var index = 0; index < 35; index++)
        {
            await _productAppService.CreateAsync(new CreateUpdateProductDto
            {
                Name = $"LABEL-PERF-{index:00}",
                Sku = $"LABEL-PERF-SKU-{index:00}",
                Barcode = $"8690000099{index:00}",
                Description = string.Empty,
                Unit = "Adet",
                ShelfLocation = string.Empty,
                PurchaseCurrency = "TRY",
                ExchangeRateAtPurchase = 1m,
                SalePrice = 100m + index,
                PricingMode = ProductPricingMode.Manual,
                TrackStock = true
            });
        }

        var results = await _labelProductLookupAppService.GetListAsync(new LabelProductLookupInput
        {
            Filter = "LABEL-PERF-",
            MaxResultCount = 30
        });

        results.Count.ShouldBe(30);
        results.ShouldAllBe(item => item.Name.StartsWith("LABEL-PERF-", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_Apply_Purchase_Discounts_In_Sequence()
    {
        ProductPriceCalculator.ApplyDiscountChain(100m, 10m, 5m, 0m, 0m)
            .ShouldBe(85.5m);
    }

    [Fact]
    public async Task Should_Calculate_Complete_Manual_Pricing_Pipeline()
    {
        await _exchangeRateRepository.InsertAsync(new ExchangeRate(Guid.NewGuid(), "USD", 40m, DateTime.UtcNow));

        var product = await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = "Pipeline Product",
            Sku = "PIPELINE-001",
            Barcode = "869000000306",
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = "D4",
            PurchaseListPrice = 30m,
            PurchaseCurrency = "USD",
            PurchaseDiscount1 = 10m,
            PurchaseDiscount2 = 5m,
            ExchangeRateAtPurchase = 40m,
            SalePrice = 2000m,
            SalesDiscount1 = 5m,
            VatRate = 20,
            PricingMode = ProductPricingMode.Manual,
            ProfitMarkupPercent = 30m,
            Quantity = 10m,
            CriticalStockLevel = 1m,
            TrackStock = true
        });

        var quote = await _productPricingAppService.GetQuoteAsync(new ProductPriceQuoteInputDto
        {
            ProductId = product.Id
        });

        quote.NetCostInTry.ShouldBe(1026m);
        quote.ListSalePriceInTry.ShouldBe(2000m);
        quote.SalesDiscountAmountInTry.ShouldBe(0m);
        quote.UnitPrice.ShouldBe(2000m);
        quote.VatAmountInTry.ShouldBe(400m);
        quote.CustomerFinalPriceInTry.ShouldBe(2400m);
        quote.EstimatedProfitInTry.ShouldBe(974m);
        quote.EstimatedNetMarginPercent.ShouldBe(48.7m);

        await WithUnitOfWorkAsync(async () =>
        {
            var usd = await _exchangeRateRepository.GetAsync(x => x.Currency == "USD");
            usd.Rate = 50m;
            await _exchangeRateRepository.UpdateAsync(usd);
        });
        var afterRateChange = await _productPricingAppService.GetQuoteAsync(
            new ProductPriceQuoteInputDto { ProductId = product.Id });
        afterRateChange.UnitPrice.ShouldBe(2000m);
    }

    [Fact]
    public async Task Should_Use_Current_Cost_Currency_Rate_And_Markup_For_Sales_Document()
    {
        await _exchangeRateRepository.InsertAsync(new ExchangeRate(Guid.NewGuid(), "EUR", 35m, DateTime.UtcNow));
        await _exchangeRateRepository.InsertAsync(new ExchangeRate(Guid.NewGuid(), "USD", 32m, DateTime.UtcNow));

        var product = await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = "Dynamic EUR Product",
            Sku = "DYN-EUR-001",
            Barcode = "869000000303",
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = "D1",
            PurchasePrice = 35m,
            PurchaseCurrency = "EUR",
            SalePrice = 1m,
            PricingMode = ProductPricingMode.DynamicCurrencyMarkup,
            ProfitMarkupPercent = 40m,
            UseCurrentExchangeRateForSale = true,
            Quantity = 0m,
            CriticalStockLevel = 1m,
            TrackStock = true
        });
        await ApprovePurchaseAsync(product.Id, "EUR", 35m, "DYN-PURCHASE-001");
        var customer = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = "Dynamic Price Customer",
            Type = BusinessPartnerType.Customer
        });

        var quote = await _productPricingAppService.GetQuoteAsync(new ProductPriceQuoteInputDto
        {
            ProductId = product.Id
        });

        quote.UnitPrice.ShouldBe(1715m);
        quote.PriceInTry.ShouldBe(1715m);
        quote.UsesCurrentExchangeRate.ShouldBeFalse();

        var document = await _documentAppService.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = customer.Id,
            DocNumber = "DYN-SALE-001",
            Type = DocumentType.SaleInvoice,
            Status = DocumentStatus.Draft,
            Currency = "TRY",
            ExchangeRate = 1m,
            Date = DateTime.UtcNow,
            Items =
            {
                new CreateUpdateDocumentItemDto
                {
                    ProductId = product.Id,
                    Quantity = 1m,
                    UnitPrice = 1m,
                    VatRate = 20m,
                    DiscountRate = 0m
                }
            }
        });

        document.ExchangeRate.ShouldBe(1m);
        document.Items.Single().UnitPrice.ShouldBe(1715m);
        document.Items.Single().PriceCurrencyCode.ShouldBe("TRY");
        document.Items.Single().ReferenceCurrencyCode.ShouldBe("EUR");
        document.Items.Single().ReferenceExchangeRate.ShouldBe(35m);
        document.GrandTotal.ShouldBe(2058m);

        var foreignCurrencyError = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
            _documentAppService.CreateAsync(new CreateUpdateDocumentDto
            {
                BusinessPartnerId = customer.Id,
                DocNumber = "DYN-SALE-USD-REJECTED",
                Type = DocumentType.SaleInvoice,
                Currency = "USD",
                Date = DateTime.UtcNow,
                Items =
                {
                    new CreateUpdateDocumentItemDto
                    {
                        ProductId = product.Id,
                        Quantity = 1m,
                        UnitPrice = 1m,
                        VatRate = 20m
                    }
                }
            }));
        foreignCurrencyError.Code.ShouldBe("NoxStock:SalesDocumentsRequireTry");
    }

    [Fact]
    public async Task Should_Preserve_Manually_Overridden_Sales_Price_On_Document()
    {
        await _exchangeRateRepository.InsertAsync(new ExchangeRate(Guid.NewGuid(), "USD", 32m, DateTime.UtcNow));

        var product = await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = "Override USD Product",
            Sku = "OVERRIDE-USD-001",
            Barcode = "869000000304",
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = "D2",
            PurchasePrice = 25m,
            PurchaseCurrency = "USD",
            SalePrice = 1m,
            PricingMode = ProductPricingMode.DynamicCurrencyMarkup,
            ProfitMarkupPercent = 40m,
            UseCurrentExchangeRateForSale = true,
            Quantity = 0m,
            CriticalStockLevel = 1m,
            TrackStock = true
        });
        await ApprovePurchaseAsync(product.Id, "USD", 25m, "OVERRIDE-PURCHASE-001");
        var customer = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = "Override Price Customer",
            Type = BusinessPartnerType.Customer
        });

        var document = await _documentAppService.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = customer.Id,
            DocNumber = "OVERRIDE-SALE-001",
            Type = DocumentType.SaleInvoice,
            Currency = "TRY",
            Date = DateTime.UtcNow,
            Items =
            {
                new CreateUpdateDocumentItemDto
                {
                    ProductId = product.Id,
                    Quantity = 1m,
                    UnitPrice = 999m,
                    IsUnitPriceOverridden = true,
                    VatRate = 20m,
                    DiscountRate = 0m
                }
            }
        });

        document.Items.Single().UnitPrice.ShouldBe(999m);
        document.GrandTotal.ShouldBe(1198.8m);
    }

    [Fact]
    public async Task Should_Return_Server_Calculated_List_Preview_In_Selected_Currency()
    {
        await _exchangeRateRepository.InsertAsync(new ExchangeRate(Guid.NewGuid(), "USD", 40m, DateTime.UtcNow));
        await _exchangeRateRepository.InsertAsync(new ExchangeRate(Guid.NewGuid(), "EUR", 50m, DateTime.UtcNow));

        var product = await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = "List Currency Preview Product",
            Sku = "LIST-PREVIEW-001",
            Barcode = "869000000305",
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = "D3",
            PurchasePrice = 0m,
            PurchaseCurrency = "USD",
            SalePrice = 0m,
            PricingMode = ProductPricingMode.DynamicCurrencyMarkup,
            ProfitMarkupPercent = 20m,
            UseCurrentExchangeRateForSale = true,
            Quantity = 0m,
            CriticalStockLevel = 1m,
            TrackStock = true
        });
        await ApprovePurchaseAsync(product.Id, "TRY", 1000m, "LIST-PREVIEW-PURCHASE-001");

        await WithUnitOfWorkAsync(async () =>
        {
            var usd = await _exchangeRateRepository.FirstOrDefaultAsync(x => x.Currency == "USD");
            usd!.Rate = 41m;
            await _exchangeRateRepository.UpdateAsync(usd);
        });

        var list = await _productAppService.GetListAsync(new ProductGetListInput
        {
            Filter = "LIST-PREVIEW-001",
            MaxResultCount = 10
        });

        var row = list.Items.Single();
        row.IsSuggestedSaleAvailable.ShouldBeTrue();
        row.SuggestedSaleUnitPrice.ShouldBe(1200m);
        row.ReferenceSaleUnitPrice.ShouldBe(29.27m);
        row.CurrentReferenceCurrencyExchangeRate.ShouldBe(41m);
        row.LastPurchaseCostCurrencyAmount.ShouldBe(25m);
    }

    [Fact]
    public async Task Should_Keep_Untracked_Products_Out_Of_Stock_Attention_Filters()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var tracked = await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = "Tracked zero stock " + suffix,
            Sku = "TRACKED-ZERO-" + suffix,
            Unit = "Adet",
            PurchaseCurrency = "TRY",
            SalePrice = 100m,
            PricingMode = ProductPricingMode.Manual,
            Quantity = 0m,
            CriticalStockLevel = 1m,
            TrackStock = true
        });
        var untracked = await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = "Untracked service " + suffix,
            Sku = "UNTRACKED-" + suffix,
            Unit = "Adet",
            PurchaseCurrency = "TRY",
            SalePrice = 100m,
            PricingMode = ProductPricingMode.Manual,
            Quantity = 0m,
            CriticalStockLevel = 1m,
            TrackStock = false
        });

        var attention = await _productAppService.GetListAsync(new ProductGetListInput
        {
            Filter = suffix,
            StockStatus = "attention",
            MaxResultCount = 10
        });
        var untrackedOnly = await _productAppService.GetListAsync(new ProductGetListInput
        {
            Filter = suffix,
            StockStatus = "untracked",
            MaxResultCount = 10
        });

        attention.Items.Select(item => item.Id).ShouldBe(new[] { tracked.Id });
        untrackedOnly.Items.Select(item => item.Id).ShouldBe(new[] { untracked.Id });
    }

    [Fact]
    public async Task Should_Keep_Product_List_Query_Count_Bounded_As_Page_Size_Grows()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            if (await _exchangeRateRepository.FirstOrDefaultAsync(x => x.Currency == "USD") is null)
            {
                await _exchangeRateRepository.InsertAsync(
                    new ExchangeRate(Guid.NewGuid(), "USD", 40m, DateTime.UtcNow));
            }
        });

        var prefix = "BATCH-" + Guid.NewGuid().ToString("N")[..8];
        for (var index = 0; index < 12; index++)
        {
            await _productAppService.CreateAsync(new CreateUpdateProductDto
            {
                Name = $"Batch list product {index}",
                Sku = $"{prefix}-{index:00}",
                Barcode = $"86990000{index:0000}",
                OemCodes = $"OEM-{prefix}-{index:00}",
                Description = string.Empty,
                Unit = "Adet",
                ShelfLocation = "P1",
                PurchaseListPrice = 10m,
                PurchaseCurrency = "USD",
                ExchangeRateAtPurchase = 40m,
                SalePrice = 600m,
                VatRate = 20,
                PricingMode = ProductPricingMode.Manual,
                ProfitMarkupPercent = 30m,
                Quantity = 10m,
                CriticalStockLevel = 1m,
                TrackStock = true
            });
        }

        SqlCommandCounter.Instance.Reset();
        var list = await _productAppService.GetListAsync(new ProductGetListInput
        {
            Filter = prefix,
            MaxResultCount = 20,
            Sorting = "Name"
        });

        list.Items.Count.ShouldBe(12);
        list.Items.ShouldAllBe(item => !string.IsNullOrWhiteSpace(item.OemCodes));
        // The interceptor also sees ABP permission/UOW infrastructure reads. The
        // important invariant is that a 12-row page stays bounded instead of
        // returning to the former one-or-more SQL commands per product.
        SqlCommandCounter.Instance.CommandCount.ShouldBeLessThanOrEqualTo(12);
    }

    [Fact]
    public async Task Should_Search_Invoice_Product_Lookups_Without_Loading_Pricing_Details()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var product = await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = "Invoice lookup product " + suffix,
            Sku = "INV-LOOKUP-" + suffix,
            Barcode = "8697" + suffix,
            OemCodes = "OEM-" + suffix,
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = "L1",
            PurchaseCurrency = "TRY",
            ExchangeRateAtPurchase = 1m,
            SalePrice = 100m,
            VatRate = 20,
            PricingMode = ProductPricingMode.Manual,
            ProfitMarkupPercent = 30m,
            Quantity = 7m,
            TrackStock = true
        });

        SqlCommandCounter.Instance.Reset();
        var result = await _productAppService.GetLookupListAsync(new ProductLookupInput
        {
            Filter = suffix,
            MaxResultCount = 20
        });

        var row = result.Items.Single(item => item.Id == product.Id);
        result.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
        row.Name.ShouldBe(product.Name);
        row.Sku.ShouldBe(product.Sku);
        row.Barcode.ShouldBe(product.Barcode);
        row.Quantity.ShouldBe(7m);
        SqlCommandCounter.Instance.CommandCount.ShouldBeLessThanOrEqualTo(7);
    }

    [Fact]
    public async Task Should_Prioritize_Exact_Invoice_Barcode_Over_Substring_Matches()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var barcode = "8698" + suffix;
        var exactProduct = await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = "Exact barcode product " + suffix,
            Sku = "EXACT-" + suffix,
            Barcode = barcode,
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = string.Empty,
            PurchaseCurrency = "TRY",
            ExchangeRateAtPurchase = 1m,
            SalePrice = 100m,
            VatRate = 20,
            PricingMode = ProductPricingMode.Manual,
            Quantity = 1m,
            TrackStock = true
        });
        await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = "Substring distractor " + barcode,
            Sku = "DISTRACTOR-" + suffix,
            Barcode = "777" + suffix,
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = string.Empty,
            PurchaseCurrency = "TRY",
            ExchangeRateAtPurchase = 1m,
            SalePrice = 100m,
            VatRate = 20,
            PricingMode = ProductPricingMode.Manual,
            Quantity = 1m,
            TrackStock = true
        });

        var result = await _productAppService.GetLookupListAsync(new ProductLookupInput
        {
            Filter = barcode,
            MaxResultCount = 20
        });

        result.TotalCount.ShouldBe(1);
        result.Items.Single().Id.ShouldBe(exactProduct.Id);
    }

    private async Task ApprovePurchaseAsync(Guid productId, string currency, decimal unitPrice, string documentNumber)
    {
        var supplier = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = documentNumber + " Supplier",
            Type = BusinessPartnerType.Supplier,
            Currency = currency
        });
        var purchase = await _documentAppService.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = supplier.Id,
            DocNumber = documentNumber,
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
                    VatRate = 0m,
                    DiscountRate = 0m
                }
            }
        });

        await _documentWorkflowAppService.ApproveAsync(purchase.Id, new ApproveDocumentInputDto());
    }
}
