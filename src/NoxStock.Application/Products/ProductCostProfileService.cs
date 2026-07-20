using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NoxStock.Documents;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace NoxStock.Products;

public interface IProductCostProfileService
{
    Task ApplyApprovedPurchaseAsync(Document document);
    Task ReverseApprovedPurchaseAsync(Document document);
}

public class ProductCostProfileService : DomainService, IProductCostProfileService, ITransientDependency
{
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<ProductCostHistory, Guid> _historyRepository;

    public ProductCostProfileService(
        IRepository<Product, Guid> productRepository,
        IRepository<ProductCostHistory, Guid> historyRepository)
    {
        _productRepository = productRepository;
        _historyRepository = historyRepository;
    }

    public async Task ApplyApprovedPurchaseAsync(Document document)
    {
        foreach (var item in document.Items)
        {
            if (await _historyRepository.AnyAsync(x =>
                    x.SourceDocumentItemId == item.Id &&
                    x.ChangeType == ProductCostHistoryChangeType.PurchaseApproved))
            {
                continue;
            }

            var product = await _productRepository.GetAsync(item.ProductId);
            var sourceCurrency = NormalizeCurrency(document.Currency);
            var pricingCurrency = NormalizeCurrency(string.IsNullOrWhiteSpace(product.PricingCurrencyCode)
                ? product.SaleCurrency
                : product.PricingCurrencyCode);
            var sourceExchangeRate = GetDocumentRate(document, sourceCurrency);
            var pricingExchangeRate = GetDocumentRate(document, pricingCurrency);
            var sourceNetUnitCost = Round(item.UnitPrice * (1 - item.DiscountRate / 100m));
            var tryEquivalentCost = Round(sourceNetUnitCost * sourceExchangeRate);
            var pricingEquivalentCost = Round(tryEquivalentCost / pricingExchangeRate);

            var history = new ProductCostHistory(GuidGenerator.Create(), CurrentTenant.Id)
            {
                ProductId = product.Id,
                SourceDocumentId = document.Id,
                SourceDocumentItemId = item.Id,
                SupplierId = document.BusinessPartnerId,
                CostDate = document.Date,
                SourceCurrencyCode = sourceCurrency,
                SourceNetUnitCost = sourceNetUnitCost,
                SourceExchangeRate = sourceExchangeRate,
                TryEquivalentNetUnitCost = tryEquivalentCost,
                PricingCurrencyCode = pricingCurrency,
                PricingCurrencyEquivalentCost = pricingEquivalentCost,
                ChangeType = ProductCostHistoryChangeType.PurchaseApproved
            };

            await _historyRepository.InsertAsync(history);
            await ApplyProfileAsync(product, history);
            await _productRepository.UpdateAsync(product);
        }
    }

    public async Task ReverseApprovedPurchaseAsync(Document document)
    {
        var approvedHistories = await _historyRepository.GetListAsync(x =>
            x.SourceDocumentId == document.Id &&
            x.ChangeType == ProductCostHistoryChangeType.PurchaseApproved);

        foreach (var approvedHistory in approvedHistories)
        {
            var cancellationExists = await _historyRepository.AnyAsync(x =>
                x.SourceDocumentItemId == approvedHistory.SourceDocumentItemId &&
                x.ChangeType == ProductCostHistoryChangeType.PurchaseCancelled);
            if (!cancellationExists)
            {
                await _historyRepository.InsertAsync(new ProductCostHistory(GuidGenerator.Create(), CurrentTenant.Id)
                {
                    ProductId = approvedHistory.ProductId,
                    SourceDocumentId = approvedHistory.SourceDocumentId,
                    SourceDocumentItemId = approvedHistory.SourceDocumentItemId,
                    SupplierId = approvedHistory.SupplierId,
                    CostDate = Clock.Now,
                    SourceCurrencyCode = approvedHistory.SourceCurrencyCode,
                    SourceNetUnitCost = approvedHistory.SourceNetUnitCost,
                    SourceExchangeRate = approvedHistory.SourceExchangeRate,
                    TryEquivalentNetUnitCost = approvedHistory.TryEquivalentNetUnitCost,
                    PricingCurrencyCode = approvedHistory.PricingCurrencyCode,
                    PricingCurrencyEquivalentCost = approvedHistory.PricingCurrencyEquivalentCost,
                    ChangeType = ProductCostHistoryChangeType.PurchaseCancelled
                });
            }

            var product = await _productRepository.GetAsync(approvedHistory.ProductId);
            await RestoreLatestActiveProfileAsync(product, approvedHistory.SourceDocumentItemId);
            await _productRepository.UpdateAsync(product);
        }
    }

    private async Task RestoreLatestActiveProfileAsync(Product product, Guid? additionallyCancelledItemId = null)
    {
        var histories = await _historyRepository.GetListAsync(x => x.ProductId == product.Id);
        var cancelledItemIds = histories
            .Where(x => x.ChangeType == ProductCostHistoryChangeType.PurchaseCancelled)
            .Select(x => x.SourceDocumentItemId)
            .ToHashSet();
        if (additionallyCancelledItemId.HasValue)
        {
            cancelledItemIds.Add(additionallyCancelledItemId.Value);
        }
        var latest = histories
            .Where(x => x.ChangeType == ProductCostHistoryChangeType.PurchaseApproved &&
                        !cancelledItemIds.Contains(x.SourceDocumentItemId))
            .OrderByDescending(x => x.CostDate)
            .ThenByDescending(x => x.CreationTime)
            .FirstOrDefault();

        if (latest is null)
        {
            product.LastPurchaseCostCurrencyCode = null;
            product.LastPurchaseCostCurrencyAmount = null;
            product.LastPurchaseCostTryAmount = null;
            product.LastPurchaseExchangeRate = null;
            product.LastPurchaseDate = null;
            product.LastPurchaseDocumentId = null;
            product.LastPurchaseDocumentItemId = null;
            product.LastPurchaseCostHistoryId = null;
            product.LastPurchaseSupplierId = null;
            product.LastCostUpdatedAt = Clock.Now;
            return;
        }

        await ApplyProfileAsync(product, latest);
    }

    private Task ApplyProfileAsync(Product product, ProductCostHistory history)
    {
        product.PricingCurrencyCode = history.PricingCurrencyCode;
        product.LastPurchaseCostCurrencyCode = history.PricingCurrencyCode;
        product.LastPurchaseCostCurrencyAmount = history.PricingCurrencyEquivalentCost;
        product.LastPurchaseCostTryAmount = history.TryEquivalentNetUnitCost;
        product.LastPurchaseExchangeRate = history.PricingCurrencyEquivalentCost > 0m
            ? Math.Round(history.TryEquivalentNetUnitCost / history.PricingCurrencyEquivalentCost, 6, MidpointRounding.AwayFromZero)
            : history.SourceExchangeRate;
        product.LastPurchaseDate = history.CostDate;
        product.LastPurchaseDocumentId = history.SourceDocumentId;
        product.LastPurchaseDocumentItemId = history.SourceDocumentItemId;
        product.LastPurchaseCostHistoryId = history.Id;
        product.LastPurchaseSupplierId = history.SupplierId;
        product.LastCostUpdatedAt = Clock.Now;
        return Task.CompletedTask;
    }

    private static decimal GetDocumentRate(Document document, string currency)
    {
        if (currency == "TRY")
        {
            return 1m;
        }

        var snapshot = document.ExchangeRateSnapshots
            .OrderByDescending(x => x.RateDate)
            .FirstOrDefault(x => string.Equals(x.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase));
        if (snapshot?.RateToTry > 0)
        {
            return snapshot.RateToTry;
        }

        if (string.Equals(document.Currency, currency, StringComparison.OrdinalIgnoreCase) && document.ExchangeRate > 0)
        {
            return document.ExchangeRate;
        }

        throw new BusinessException("NoxStock:DocumentExchangeRateSnapshotUnavailable")
            .WithData("Currency", currency ?? string.Empty)
            .WithData("DocumentNumber", document.DocNumber);
    }

    private static string NormalizeCurrency(string? currency)
    {
        var normalized = currency?.Trim().ToUpperInvariant();
        if (normalized is not ("TRY" or "USD" or "EUR"))
        {
            throw new BusinessException("NoxStock:UnsupportedCurrency").WithData("Currency", currency);
        }

        return normalized;
    }

    private static decimal Round(decimal value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);
}
