using System;
using System.Threading.Tasks;
using NoxStock.ExchangeRates;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace NoxStock.Products;

public interface IProductPriceCalculator
{
    Task<ProductPriceCalculation> CalculateAsync(Product product, string targetCurrency);
    Task<decimal> GetExchangeRateAsync(string currency);
}

public sealed class ProductPriceCalculation
{
    public bool IsAvailable { get; init; } = true;
    public string? UnavailableReason { get; init; }
    public string Currency { get; init; } = "TRY";
    public decimal UnitPrice { get; init; }
    public decimal PriceInTry { get; init; }
    public decimal ExchangeRate { get; init; } = 1;
    public DateTime? RateDate { get; init; }
    public bool UsesCurrentExchangeRate { get; init; }
    public string PricingCurrencyCode { get; init; } = "TRY";
    public decimal PricingCurrencyExchangeRate { get; init; } = 1;
    public DateTime? PricingCurrencyRateDate { get; init; }
    public decimal? LastPurchaseCostCurrencyAmount { get; init; }
    public decimal? LastPurchaseCostTryAmount { get; init; }
    public decimal? ReferenceSaleUnitPrice { get; init; }
    public decimal MarkupPercent { get; init; }
    public Guid? ProductCostHistoryId { get; init; }
}

public class ProductPriceCalculator : IProductPriceCalculator, ITransientDependency
{
    private readonly IRepository<ExchangeRate, Guid> _exchangeRateRepository;

    public ProductPriceCalculator(IRepository<ExchangeRate, Guid> exchangeRateRepository)
    {
        _exchangeRateRepository = exchangeRateRepository;
    }

    public async Task<ProductPriceCalculation> CalculateAsync(Product product, string targetCurrency)
    {
        targetCurrency = NormalizeCurrency(targetCurrency);
        var targetRate = await GetExchangeRateAsync(targetCurrency);
        var targetRateDate = await GetRateDateAsync(targetCurrency);

        var pricingCurrency = NormalizeCurrency(string.IsNullOrWhiteSpace(product.PricingCurrencyCode)
            ? product.SaleCurrency
            : product.PricingCurrencyCode);
        var markupPercent = product.ProfitMarkupPercent > 0 ? product.ProfitMarkupPercent : 30m;
        var hasLastPurchaseCost = product.LastPurchaseCostCurrencyAmount is > 0;
        var usesCostProfile = product.PricingMode == ProductPricingMode.DynamicCurrencyMarkup && hasLastPurchaseCost;

        if (product.PricingMode == ProductPricingMode.DynamicCurrencyMarkup && !hasLastPurchaseCost)
        {
            return new ProductPriceCalculation
            {
                IsAvailable = false,
                UnavailableReason = "NoxStock:ProductApprovedPurchaseCostUnavailable",
                Currency = targetCurrency,
                ExchangeRate = targetRate,
                RateDate = targetRateDate,
                UsesCurrentExchangeRate = true,
                PricingCurrencyCode = pricingCurrency,
                PricingCurrencyExchangeRate = await GetExchangeRateAsync(pricingCurrency),
                PricingCurrencyRateDate = await GetRateDateAsync(pricingCurrency),
                MarkupPercent = markupPercent
            };
        }

        if (!usesCostProfile)
        {
            var fixedCurrency = NormalizeCurrency(string.IsNullOrWhiteSpace(product.SaleCurrency) ? "TRY" : product.SaleCurrency);
            var fixedRate = await GetExchangeRateAsync(fixedCurrency);
            var fixedPriceInTry = product.SalePrice * fixedRate;

            return new ProductPriceCalculation
            {
                Currency = targetCurrency,
                UnitPrice = Round(fixedPriceInTry / targetRate),
                PriceInTry = Round(fixedPriceInTry),
                ExchangeRate = targetRate,
                RateDate = targetRateDate,
                UsesCurrentExchangeRate = false,
                PricingCurrencyCode = pricingCurrency,
                PricingCurrencyExchangeRate = await GetExchangeRateAsync(pricingCurrency),
                PricingCurrencyRateDate = await GetRateDateAsync(pricingCurrency),
                LastPurchaseCostCurrencyAmount = product.LastPurchaseCostCurrencyAmount,
                LastPurchaseCostTryAmount = product.LastPurchaseCostTryAmount,
                ReferenceSaleUnitPrice = product.LastPurchaseCostCurrencyAmount is > 0
                    ? Round(product.LastPurchaseCostCurrencyAmount.Value * (1 + markupPercent / 100m))
                    : null,
                MarkupPercent = markupPercent,
                ProductCostHistoryId = product.LastPurchaseCostHistoryId
            };
        }

        if (usesCostProfile)
        {
            var pricingRate = await GetExchangeRateAsync(pricingCurrency);
            var dynamicPriceInTry = product.LastPurchaseCostCurrencyAmount!.Value * pricingRate *
                (1 + markupPercent / 100m);

            return new ProductPriceCalculation
            {
                Currency = targetCurrency,
                UnitPrice = Round(dynamicPriceInTry / targetRate),
                PriceInTry = Round(dynamicPriceInTry),
                ExchangeRate = targetRate,
                RateDate = targetRateDate,
                UsesCurrentExchangeRate = true,
                PricingCurrencyCode = pricingCurrency,
                PricingCurrencyExchangeRate = pricingRate,
                PricingCurrencyRateDate = await GetRateDateAsync(pricingCurrency),
                LastPurchaseCostCurrencyAmount = product.LastPurchaseCostCurrencyAmount,
                LastPurchaseCostTryAmount = product.LastPurchaseCostTryAmount,
                ReferenceSaleUnitPrice = Round(product.LastPurchaseCostCurrencyAmount.Value * (1 + markupPercent / 100m)),
                MarkupPercent = markupPercent,
                ProductCostHistoryId = product.LastPurchaseCostHistoryId
            };
        }

        throw new InvalidOperationException("Unsupported product pricing state.");
    }

    public async Task<decimal> GetExchangeRateAsync(string currency)
    {
        currency = NormalizeCurrency(currency);
        if (currency == "TRY")
        {
            return 1m;
        }

        var rate = await _exchangeRateRepository.FirstOrDefaultAsync(x => x.Currency == currency);
        if (rate is null || rate.Rate <= 0)
        {
            throw new BusinessException("NoxStock:CurrentExchangeRateUnavailable")
                .WithData("Currency", currency);
        }

        return rate.Rate;
    }

    private async Task<DateTime?> GetRateDateAsync(string currency)
    {
        currency = NormalizeCurrency(currency);
        if (currency == "TRY")
        {
            return null;
        }

        return (await _exchangeRateRepository.FirstOrDefaultAsync(x => x.Currency == currency))?.RateDate;
    }

    private static string NormalizeCurrency(string currency)
    {
        var normalized = currency.Trim().ToUpperInvariant();
        if (normalized is not ("TRY" or "USD" or "EUR"))
        {
            throw new BusinessException("NoxStock:UnsupportedCurrency").WithData("Currency", currency);
        }

        return normalized;
    }

    private static decimal Round(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
