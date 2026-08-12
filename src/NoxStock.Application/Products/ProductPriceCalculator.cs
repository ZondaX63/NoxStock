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
        var referenceRate = referenceCurrency == "TRY"
            ? 1m
            : currentReferenceRate?.Rate is > 0m
                ? currentReferenceRate.Rate
                : product.ExchangeRateAtPurchase > 0m
                    ? product.ExchangeRateAtPurchase
                    : 1m;
        var referenceRateDate = currentReferenceRate?.RateDate;
        var markupPercent = Round(product.ProfitMarkupPercent);
        var cardPurchaseRate = referenceCurrency == "TRY"
            ? 1m
            : product.ExchangeRateAtPurchase > 0m
                ? product.ExchangeRateAtPurchase
                : referenceRate;
        var cardPurchaseListPrice = product.PurchaseListPrice > 0m
            ? product.PurchaseListPrice
            : product.PurchasePrice;
        var manualCardCostTry = ApplyDiscountChain(
            cardPurchaseListPrice,
            product.PurchaseDiscount1,
            product.PurchaseDiscount2,
            product.PurchaseDiscount3,
            product.PurchaseDiscount4) * cardPurchaseRate;

        // Elde stok ve hesaplanmış ortalama varsa satış kararının maliyet
        // otoritesi odur. Ürün kartındaki maliyet yalnız stok maliyeti henüz
        // oluşmamışken kullanılabilecek bir fiyatlama girdisidir.
        var costTry = product.Quantity > 0m && product.AverageInventoryCostTry is > 0m
            ? product.AverageInventoryCostTry.Value
            : manualCardCostTry > 0m
                ? manualCardCostTry
                : product.AverageInventoryCostTry ?? product.LastPurchaseCostTryAmount ?? 0m;
        costTry = Round(costTry);
        var suggestedPriceTry = CalculateSuggestedSalePrice(costTry, markupPercent);
        var isAutomatic = product.PricingMode == ProductPricingMode.DynamicCurrencyMarkup;
        var activePriceTry = isAutomatic ? suggestedPriceTry : Round(product.SalePrice);
        var profit = Round(activePriceTry - costTry);

        return new ProductPriceCalculation
        {
            IsAvailable = isAutomatic ? suggestedPriceTry > 0m : activePriceTry > 0m,
            UnavailableReason = costTry <= 0m
                ? "NoxStock:ProductCostUnavailable"
                : !isAutomatic && activePriceTry <= 0m
                    ? "NoxStock:ManualSalePriceUnavailable"
                    : null,
            Currency = "TRY",
            UnitPrice = activePriceTry,
            PriceInTry = activePriceTry,
            ExchangeRate = 1m,
            RateDate = null,
            UsesCurrentExchangeRate = false,
            ReferenceCurrencyCode = referenceCurrency,
            ReferenceCurrencyExchangeRate = referenceRate,
            ReferenceCurrencyRateDate = referenceRateDate,
            LastPurchaseCostCurrencyAmount = product.LastPurchaseCostCurrencyAmount,
            LastPurchaseCostTryAmount = product.LastPurchaseCostTryAmount,
            ReferenceSaleUnitPrice = referenceRate > 0m ? Round(suggestedPriceTry / referenceRate) : null,
            MarkupPercent = markupPercent,
            ProductCostHistoryId = product.LastPurchaseCostHistoryId,
            NetCostInTry = costTry,
            ListSalePriceInTry = activePriceTry,
            SalesDiscountAmountInTry = 0m,
            VatAmountInTry = Round(activePriceTry * product.VatRate / 100m),
            CustomerFinalPriceInTry = Round(activePriceTry * (1m + product.VatRate / 100m)),
            EstimatedProfitInTry = profit,
            EstimatedNetMarginPercent = CalculateMargin(activePriceTry, costTry),
            RealizedMarkupPercent = CalculateMarkup(activePriceTry, costTry),
            GrossProfitMarginPercent = CalculateMargin(activePriceTry, costTry)
        };
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

    public static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal CalculateSuggestedSalePrice(decimal costTry, decimal markupPercent) =>
        RoundMoney(costTry * (1m + markupPercent / 100m));

    private static decimal Round(decimal value) => RoundMoney(value);

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
