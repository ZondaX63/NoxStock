using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using NoxStock.MultiTenancy;
using NoxStock.Permissions;

namespace NoxStock.Products;

[Authorize(NoxStockPermissions.Products.Default)]
public class ProductPricingAppService : ApplicationService, IProductPricingAppService
{
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IProductPriceCalculator _priceCalculator;

    public ProductPricingAppService(
        IRepository<Product, Guid> productRepository,
        IProductPriceCalculator priceCalculator)
    {
        _productRepository = productRepository;
        _priceCalculator = priceCalculator;
    }

    public async Task<ProductPriceQuoteDto> GetQuoteAsync(ProductPriceQuoteInputDto input)
    {
        CurrentTenant.RequireTenantId();
        var product = await _productRepository.GetAsync(input.ProductId);
        var calculation = await _priceCalculator.CalculateAsync(product, input.Currency);

        return new ProductPriceQuoteDto
        {
            ProductId = product.Id,
            Currency = calculation.Currency,
            UnitPrice = calculation.UnitPrice,
            PriceInTry = calculation.PriceInTry,
            ExchangeRate = calculation.ExchangeRate,
            RateDate = calculation.RateDate,
            UsesCurrentExchangeRate = calculation.UsesCurrentExchangeRate,
            ProfitMarkupPercent = product.ProfitMarkupPercent,
            PurchasePrice = product.PurchasePrice,
            PurchaseCurrency = product.PurchaseCurrency,
            SalePrice = product.SalePrice,
            SaleCurrency = product.SaleCurrency,
            LastPurchaseCostCurrencyCode = product.LastPurchaseCostCurrencyCode,
            LastPurchaseCostCurrencyAmount = product.LastPurchaseCostCurrencyAmount,
            LastPurchaseCostTryAmount = product.LastPurchaseCostTryAmount,
            PricingCurrencyExchangeRate = calculation.PricingCurrencyExchangeRate,
            ProductCostHistoryId = calculation.ProductCostHistoryId,
            PricingMode = product.PricingMode
        };
    }
}
