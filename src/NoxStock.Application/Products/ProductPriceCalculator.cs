using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NoxStock.ExchangeRates;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace NoxStock.Products;

public interface IProductPriceCalculator
{
    Task<ProductPriceCalculation> CalculateAsync(Product product);
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
    public string ReferenceCurrencyCode { get; init; } = "TRY";
    public decimal ReferenceCurrencyExchangeRate { get; init; } = 1;
    public DateTime? ReferenceCurrencyRateDate { get; init; }
    public decimal? LastPurchaseCostCurrencyAmount { get; init; }
    public decimal? LastPurchaseCostTryAmount { get; init; }
    public decimal? ReferenceSaleUnitPrice { get; init; }
    public decimal MarkupPercent { get; init; }
    public Guid? ProductCostHistoryId { get; init; }
    public decimal NetCostInTry { get; init; }
    public decimal ListSalePriceInTry { get; init; }
    public decimal SalesDiscountAmountInTry { get; init; }
    public decimal VatAmountInTry { get; init; }
    public decimal CustomerFinalPriceInTry { get; init; }
    public decimal EstimatedProfitInTry { get; init; }
    public decimal EstimatedNetMarginPercent { get; init; }
    public decimal RealizedMarkupPercent { get; init; }
    public decimal GrossProfitMarginPercent { get; init; }
}

public class ProductPriceCalculator : IProductPriceCalculator, ITransientDependency
{
    private readonly IRepository<ExchangeRate, Guid> _exchangeRateRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly Dictionary<string, ExchangeRate?> _requestRateCache =
        new(StringComparer.OrdinalIgnoreCase);
    private Guid? _rateCacheUnitOfWorkId;

    public ProductPriceCalculator(
        IRepository<ExchangeRate, Guid> exchangeRateRepository,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _exchangeRateRepository = exchangeRateRepository;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public async Task<ProductPriceCalculation> CalculateAsync(Product product)
    {
        var referenceCurrency = NormalizeCurrency(product.PurchaseCurrency);
        var currentReferenceRate = referenceCurrency == "TRY"
            ? null
            : await GetCurrentRateAsync(referenceCurrency);
        if (product.PricingMode == ProductPricingMode.DynamicCurrencyMarkup &&
            referenceCurrency != "TRY" &&
            (currentReferenceRate is null || currentReferenceRate.Rate <= 0))
        {
            throw new BusinessException("NoxStock:CurrentExchangeRateUnavailable")
                .WithData("Currency", referenceCurrency);
        }
        var referenceRate = referenceCurrency == "TRY"
            ? 1m
            : currentReferenceRate?.Rate is > 0m
                ? currentReferenceRate.Rate
                : product.ExchangeRateAtPurchase > 0m
                    ? product.ExchangeRateAtPurchase
                    : 0m;
        var referenceRateDate = currentReferenceRate?.RateDate;
        var markupPercent = product.ProfitMarkupPercent;
        var hasLastPurchaseCost = product.LastPurchaseCostCurrencyAmount is > 0;
        var usesCostProfile = product.PricingMode == ProductPricingMode.DynamicCurrencyMarkup && hasLastPurchaseCost;

        if (product.PricingMode == ProductPricingMode.DynamicCurrencyMarkup && !hasLastPurchaseCost)
        {
            return new ProductPriceCalculation
            {
                IsAvailable = false,
                UnavailableReason = "NoxStock:ProductApprovedPurchaseCostUnavailable",
                Currency = "TRY",
                ExchangeRate = 1m,
                RateDate = referenceRateDate,
                UsesCurrentExchangeRate = true,
                ReferenceCurrencyCode = referenceCurrency,
                ReferenceCurrencyExchangeRate = referenceRate,
                ReferenceCurrencyRateDate = referenceRateDate,
                MarkupPercent = markupPercent
            };
        }

        if (!usesCostProfile)
        {
            var listPriceInTry = product.SalePrice;
            var fixedPriceInTry = ApplyDiscountChain(listPriceInTry,
                product.SalesDiscount1, product.SalesDiscount2, product.SalesDiscount3, product.SalesDiscount4);
            var cardPurchaseRate = referenceCurrency == "TRY"
                ? 1m
                : product.ExchangeRateAtPurchase > 0
                    ? product.ExchangeRateAtPurchase
                    : referenceRate;
            var cardPurchaseListPrice = product.PurchaseListPrice > 0 ? product.PurchaseListPrice : product.PurchasePrice;
            var cardCostInTry = product.LastPurchaseCostTryAmount is > 0
                ? product.LastPurchaseCostTryAmount.Value
                : ApplyDiscountChain(cardPurchaseListPrice,
                    product.PurchaseDiscount1, product.PurchaseDiscount2, product.PurchaseDiscount3, product.PurchaseDiscount4) * cardPurchaseRate;

            return new ProductPriceCalculation
            {
                Currency = "TRY",
                UnitPrice = Round(fixedPriceInTry),
                PriceInTry = Round(fixedPriceInTry),
                ExchangeRate = 1m,
                RateDate = referenceRateDate,
                UsesCurrentExchangeRate = false,
                ReferenceCurrencyCode = referenceCurrency,
                ReferenceCurrencyExchangeRate = referenceRate,
                ReferenceCurrencyRateDate = referenceRateDate,
                LastPurchaseCostCurrencyAmount = product.LastPurchaseCostCurrencyAmount,
                LastPurchaseCostTryAmount = product.LastPurchaseCostTryAmount,
                ReferenceSaleUnitPrice = product.LastPurchaseCostCurrencyAmount is > 0
                    ? Round(product.LastPurchaseCostCurrencyAmount.Value * (1 + markupPercent / 100m))
                    : null,
                MarkupPercent = markupPercent,
                ProductCostHistoryId = product.LastPurchaseCostHistoryId,
                NetCostInTry = Round(cardCostInTry),
                ListSalePriceInTry = Round(listPriceInTry),
                SalesDiscountAmountInTry = Round(listPriceInTry - fixedPriceInTry),
                VatAmountInTry = Round(fixedPriceInTry * product.VatRate / 100m),
                CustomerFinalPriceInTry = Round(fixedPriceInTry * (1 + product.VatRate / 100m)),
                EstimatedProfitInTry = Round(fixedPriceInTry - cardCostInTry),
                EstimatedNetMarginPercent = CalculateMargin(fixedPriceInTry, cardCostInTry),
                RealizedMarkupPercent = CalculateMarkup(fixedPriceInTry, cardCostInTry),
                GrossProfitMarginPercent = CalculateMargin(fixedPriceInTry, cardCostInTry)
            };
        }

        if (usesCostProfile)
        {
            var netCostInTry = product.LastPurchaseCostCurrencyAmount!.Value * referenceRate;
            var listPriceInTry = netCostInTry *
                (1 + markupPercent / 100m);
            var dynamicPriceInTry = ApplyDiscountChain(listPriceInTry,
                product.SalesDiscount1, product.SalesDiscount2, product.SalesDiscount3, product.SalesDiscount4);

            return new ProductPriceCalculation
            {
                Currency = "TRY",
                UnitPrice = Round(dynamicPriceInTry),
                PriceInTry = Round(dynamicPriceInTry),
                ExchangeRate = 1m,
                RateDate = referenceRateDate,
                UsesCurrentExchangeRate = true,
                ReferenceCurrencyCode = referenceCurrency,
                ReferenceCurrencyExchangeRate = referenceRate,
                ReferenceCurrencyRateDate = referenceRateDate,
                LastPurchaseCostCurrencyAmount = product.LastPurchaseCostCurrencyAmount,
                LastPurchaseCostTryAmount = product.LastPurchaseCostTryAmount,
                ReferenceSaleUnitPrice = Round(product.LastPurchaseCostCurrencyAmount.Value * (1 + markupPercent / 100m)),
                MarkupPercent = markupPercent,
                ProductCostHistoryId = product.LastPurchaseCostHistoryId,
                NetCostInTry = Round(netCostInTry),
                ListSalePriceInTry = Round(listPriceInTry),
                SalesDiscountAmountInTry = Round(listPriceInTry - dynamicPriceInTry),
                VatAmountInTry = Round(dynamicPriceInTry * product.VatRate / 100m),
                CustomerFinalPriceInTry = Round(dynamicPriceInTry * (1 + product.VatRate / 100m)),
                EstimatedProfitInTry = Round(dynamicPriceInTry - netCostInTry),
                EstimatedNetMarginPercent = CalculateMargin(dynamicPriceInTry, netCostInTry),
                RealizedMarkupPercent = CalculateMarkup(dynamicPriceInTry, netCostInTry),
                GrossProfitMarginPercent = CalculateMargin(dynamicPriceInTry, netCostInTry)
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

        var rate = await GetCurrentRateAsync(currency);
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

        return (await GetCurrentRateAsync(currency))?.RateDate;
    }

    private async Task<ExchangeRate?> GetCurrentRateAsync(string currency)
    {
        var currentUnitOfWorkId = _unitOfWorkManager.Current?.Id;
        if (!currentUnitOfWorkId.HasValue)
        {
            return await _exchangeRateRepository.FirstOrDefaultAsync(x => x.Currency == currency);
        }

        if (_rateCacheUnitOfWorkId != currentUnitOfWorkId)
        {
            _requestRateCache.Clear();
            _rateCacheUnitOfWorkId = currentUnitOfWorkId;
        }

        if (_requestRateCache.TryGetValue(currency, out var cached))
        {
            return cached;
        }

        var rate = await _exchangeRateRepository.FirstOrDefaultAsync(x => x.Currency == currency);
        _requestRateCache[currency] = rate;
        return rate;
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

    public static decimal ApplyDiscountChain(decimal value, params decimal[] discounts)
    {
        foreach (var discount in discounts)
        {
            value *= 1m - discount / 100m;
        }

        return Round(value);
    }

    private static decimal CalculateMargin(decimal netSale, decimal cost)
    {
        return netSale <= 0m ? 0m : Round((netSale - cost) / netSale * 100m);
    }

    private static decimal CalculateMarkup(decimal netSale, decimal cost)
    {
        return cost <= 0m ? 0m : Round((netSale - cost) / cost * 100m);
    }
}
