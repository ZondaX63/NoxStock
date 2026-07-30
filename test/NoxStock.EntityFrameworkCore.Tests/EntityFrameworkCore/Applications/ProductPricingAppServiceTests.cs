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

public class ProductPricingAppServiceTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly IProductAppService _productAppService;
    private readonly IProductPricingAppService _productPricingAppService;
    private readonly IBusinessPartnerAppService _businessPartnerAppService;
    private readonly IDocumentAppService _documentAppService;
    private readonly IDocumentWorkflowAppService _documentWorkflowAppService;
    private readonly IRepository<ExchangeRate, Guid> _exchangeRateRepository;

    public ProductPricingAppServiceTests()
    {
        _productAppService = ServiceProvider.GetRequiredService<IProductAppService>();
        _productPricingAppService = ServiceProvider.GetRequiredService<IProductPricingAppService>();
        _businessPartnerAppService = ServiceProvider.GetRequiredService<IBusinessPartnerAppService>();
        _documentAppService = ServiceProvider.GetRequiredService<IDocumentAppService>();
        _documentWorkflowAppService = ServiceProvider.GetRequiredService<IDocumentWorkflowAppService>();
        _exchangeRateRepository = ServiceProvider.GetRequiredService<IRepository<ExchangeRate, Guid>>();
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
        quote.SalesDiscountAmountInTry.ShouldBe(100m);
        quote.UnitPrice.ShouldBe(1900m);
        quote.VatAmountInTry.ShouldBe(380m);
        quote.CustomerFinalPriceInTry.ShouldBe(2280m);
        quote.EstimatedProfitInTry.ShouldBe(874m);
        quote.EstimatedNetMarginPercent.ShouldBe(46m);

        await WithUnitOfWorkAsync(async () =>
        {
            var usd = await _exchangeRateRepository.GetAsync(x => x.Currency == "USD");
            usd.Rate = 50m;
            await _exchangeRateRepository.UpdateAsync(usd);
        });
        var afterRateChange = await _productPricingAppService.GetQuoteAsync(
            new ProductPriceQuoteInputDto { ProductId = product.Id });
        afterRateChange.UnitPrice.ShouldBe(1900m);
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
            Quantity = 10m,
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
        quote.UsesCurrentExchangeRate.ShouldBeTrue();

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
            Quantity = 10m,
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
            Quantity = 10m,
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
        row.SuggestedSaleUnitPrice.ShouldBe(1230m);
        row.ReferenceSaleUnitPrice.ShouldBe(30m);
        row.CurrentReferenceCurrencyExchangeRate.ShouldBe(41m);
        row.LastPurchaseCostCurrencyAmount.ShouldBe(25m);
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
