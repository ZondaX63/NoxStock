using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NexStock.BusinessPartners;
using NexStock.Documents;
using NexStock.ExchangeRates;
using NexStock.Products;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace NexStock.EntityFrameworkCore.Applications;

public class ProductPricingAppServiceTests : NexStockEntityFrameworkCoreTestBase
{
    private readonly IProductAppService _productAppService;
    private readonly IProductPricingAppService _productPricingAppService;
    private readonly IBusinessPartnerAppService _businessPartnerAppService;
    private readonly IDocumentAppService _documentAppService;
    private readonly IRepository<ExchangeRate, Guid> _exchangeRateRepository;

    public ProductPricingAppServiceTests()
    {
        _productAppService = ServiceProvider.GetRequiredService<IProductAppService>();
        _productPricingAppService = ServiceProvider.GetRequiredService<IProductPricingAppService>();
        _businessPartnerAppService = ServiceProvider.GetRequiredService<IBusinessPartnerAppService>();
        _documentAppService = ServiceProvider.GetRequiredService<IDocumentAppService>();
        _exchangeRateRepository = ServiceProvider.GetRequiredService<IRepository<ExchangeRate, Guid>>();
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
            SaleCurrency = "TRY",
            ProfitMarkupPercent = 40m,
            UseCurrentExchangeRateForSale = true,
            Quantity = 10m,
            CriticalStockLevel = 1m,
            TrackStock = true,
            Currency = "TRY"
        });
        var customer = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = "Dynamic Price Customer",
            Type = BusinessPartnerType.Customer,
            Currency = "TRY"
        });

        var quote = await _productPricingAppService.GetQuoteAsync(new ProductPriceQuoteInputDto
        {
            ProductId = product.Id,
            Currency = "TRY"
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
        document.GrandTotal.ShouldBe(2058m);
    }
}
