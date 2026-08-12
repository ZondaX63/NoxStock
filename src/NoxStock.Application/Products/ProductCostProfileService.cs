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
        foreach (var productItems in document.Items.GroupBy(x => x.ProductId))
        {
            var pendingItems = new List<DocumentItem>();
            foreach (var item in productItems)
            {
                if (!await _historyRepository.AnyAsync(x =>
                        x.SourceDocumentItemId == item.Id &&
                        x.ChangeType == ProductCostHistoryChangeType.PurchaseApproved))
                {
                    pendingItems.Add(item);
                }
            }
            if (pendingItems.Count == 0) continue;

            var product = await _productRepository.GetAsync(productItems.Key);
            var sourceCurrency = NormalizeCurrency(document.Currency);
            var referenceCurrency = NormalizeCurrency(product.PurchaseCurrency);
            var sourceExchangeRate = GetDocumentRate(document, sourceCurrency);
            var referenceExchangeRate = GetDocumentRate(document, referenceCurrency);
            var incomingQuantity = pendingItems.Sum(x => x.Quantity);
            var quantityAfter = product.Quantity;
            var quantityBefore = Math.Max(0m, quantityAfter - incomingQuantity);
            var averageBefore = ResolveKnownCostBeforePurchase(product, quantityBefore);
            var incomingTryTotal = pendingItems.Sum(item =>
                RoundMoney(ApplyPurchaseDiscountChain(item) * sourceExchangeRate) * item.Quantity);
            var inventoryValueBefore = quantityBefore * (averageBefore ?? 0m);
            var averageAfter = quantityAfter > 0m
                ? RoundMoney((inventoryValueBefore + incomingTryTotal) / quantityAfter)
                : (decimal?)null;

            ProductCostHistory? latestHistory = null;
            foreach (var item in pendingItems.OrderBy(x => x.CreationTime).ThenBy(x => x.Id))
            {
                var sourceNetUnitCost = ApplyPurchaseDiscountChain(item);
                var tryEquivalentCost = RoundMoney(sourceNetUnitCost * sourceExchangeRate);
                var referenceEquivalentCost = RoundMoney(tryEquivalentCost / referenceExchangeRate);
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
                    ReferenceCurrencyCode = referenceCurrency,
                    ReferenceCurrencyEquivalentCost = referenceEquivalentCost,
                    Quantity = item.Quantity,
                    QuantityBefore = quantityBefore,
                    AverageCostBeforeTry = averageBefore,
                    QuantityAfter = quantityAfter,
                    AverageCostAfterTry = averageAfter,
                    ChangeType = ProductCostHistoryChangeType.PurchaseApproved
                };

                item.PriceCurrencyCode = sourceCurrency;
                item.PriceExchangeRate = sourceExchangeRate;
                item.PriceRateDate = document.Date;
                item.UnitPriceTry = RoundMoney(item.UnitPrice * sourceExchangeRate);
                item.CostCurrencyCode = sourceCurrency;
                item.CostCurrencyAmount = sourceNetUnitCost;
                item.CostTryAmount = tryEquivalentCost;
                item.TotalCostTryAmount = RoundMoney(tryEquivalentCost * item.Quantity);
                item.ProductCostHistoryId = history.Id;
                item.PricingSnapshotDate = Clock.Now;

                await _historyRepository.InsertAsync(history);
                latestHistory = history;
            }

            product.AverageInventoryCostTry = averageAfter;
            product.InventoryCostUpdatedAt = Clock.Now;
            if (latestHistory is not null)
            {
                await ApplyProfileAsync(product, latestHistory);
            }
            ApplyAutomaticSalePrice(product);
            await _productRepository.UpdateAsync(product);
        }
    }

    public async Task ReverseApprovedPurchaseAsync(Document document)
    {
        var approvedHistories = await _historyRepository.GetListAsync(x =>
            x.SourceDocumentId == document.Id &&
            x.ChangeType == ProductCostHistoryChangeType.PurchaseApproved);

        foreach (var productHistories in approvedHistories.GroupBy(x => x.ProductId))
        {
            var product = await _productRepository.GetAsync(productHistories.Key);
            var cancelledQuantity = 0m;
            var cancelledValue = 0m;
            foreach (var approvedHistory in productHistories)
            {
                var cancellationExists = await _historyRepository.AnyAsync(x =>
                    x.SourceDocumentItemId == approvedHistory.SourceDocumentItemId &&
                    x.ChangeType == ProductCostHistoryChangeType.PurchaseCancelled);
                if (cancellationExists) continue;

                var quantity = approvedHistory.Quantity;
                if (quantity <= 0m)
                {
                    quantity = document.Items.FirstOrDefault(x => x.Id == approvedHistory.SourceDocumentItemId)?.Quantity ?? 0m;
                }
                cancelledQuantity += quantity;
                cancelledValue += quantity * approvedHistory.TryEquivalentNetUnitCost;

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
                    ReferenceCurrencyCode = approvedHistory.ReferenceCurrencyCode,
                    ReferenceCurrencyEquivalentCost = approvedHistory.ReferenceCurrencyEquivalentCost,
                    Quantity = quantity,
                    ChangeType = ProductCostHistoryChangeType.PurchaseCancelled
                });
            }

            // Stok ters kaydı bu servis çağrılmadan önce işlendi. İptal edilen
            // girişin stok değeri mevcut değerden çıkarılarak ortalama korunur.
            if (cancelledQuantity > 0m && product.AverageInventoryCostTry is > 0m)
            {
                var quantityBeforeReversal = product.Quantity + cancelledQuantity;
                var valueAfter = quantityBeforeReversal * product.AverageInventoryCostTry.Value - cancelledValue;
                product.AverageInventoryCostTry = product.Quantity > 0m
                    ? RoundMoney(Math.Max(0m, valueAfter) / product.Quantity)
                    : null;
                product.InventoryCostUpdatedAt = Clock.Now;
            }

            await RestoreLatestActiveProfileAsync(
                product,
                productHistories.Select(x => x.SourceDocumentItemId).ToHashSet());
            ApplyAutomaticSalePrice(product);
            await _productRepository.UpdateAsync(product);
        }
    }

    private async Task RestoreLatestActiveProfileAsync(Product product, ISet<Guid>? additionallyCancelledItemIds = null)
    {
        var histories = await _historyRepository.GetListAsync(x => x.ProductId == product.Id);
        var cancelledItemIds = histories
            .Where(x => x.ChangeType == ProductCostHistoryChangeType.PurchaseCancelled)
            .Select(x => x.SourceDocumentItemId)
            .ToHashSet();
        if (additionallyCancelledItemIds is not null)
        {
            cancelledItemIds.UnionWith(additionallyCancelledItemIds);
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
        product.LastPurchaseCostCurrencyCode = history.ReferenceCurrencyCode;
        product.LastPurchaseCostCurrencyAmount = history.ReferenceCurrencyEquivalentCost;
        product.LastPurchaseCostTryAmount = history.TryEquivalentNetUnitCost;
        product.LastPurchaseExchangeRate = history.ReferenceCurrencyEquivalentCost > 0m
            ? Math.Round(history.TryEquivalentNetUnitCost / history.ReferenceCurrencyEquivalentCost, 6, MidpointRounding.AwayFromZero)
            : history.SourceExchangeRate;
        product.LastPurchaseDate = history.CostDate;
        product.LastPurchaseDocumentId = history.SourceDocumentId;
        product.LastPurchaseDocumentItemId = history.SourceDocumentItemId;
        product.LastPurchaseCostHistoryId = history.Id;
        product.LastPurchaseSupplierId = history.SupplierId;
        product.LastCostUpdatedAt = Clock.Now;
        return Task.CompletedTask;
    }

    private static decimal? ResolveKnownCostBeforePurchase(Product product, decimal quantityBefore)
    {
        if (quantityBefore <= 0m) return 0m;
        if (product.AverageInventoryCostTry is > 0m) return product.AverageInventoryCostTry.Value;
        if (product.LastPurchaseCostTryAmount is > 0m) return product.LastPurchaseCostTryAmount.Value;

        var listPrice = product.PurchaseListPrice > 0m ? product.PurchaseListPrice : product.PurchasePrice;
        var cardCost = listPrice *
            (1m - product.PurchaseDiscount1 / 100m) *
            (1m - product.PurchaseDiscount2 / 100m) *
            (1m - product.PurchaseDiscount3 / 100m) *
            (1m - product.PurchaseDiscount4 / 100m) *
            (product.PurchaseCurrency == "TRY" ? 1m : product.ExchangeRateAtPurchase);
        if (cardCost > 0m) return RoundMoney(cardCost);

        throw new BusinessException("NoxStock:ExistingInventoryCostUnavailable")
            .WithData("ProductName", product.Name)
            .WithData("Quantity", quantityBefore);
    }

    private static void ApplyAutomaticSalePrice(Product product)
    {
        if (product.PricingMode == ProductPricingMode.DynamicCurrencyMarkup &&
            product.AverageInventoryCostTry is > 0m)
        {
            product.SalePrice = ProductPriceCalculator.CalculateSuggestedSalePrice(
                product.AverageInventoryCostTry.Value,
                product.ProfitMarkupPercent);
        }
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

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal ApplyPurchaseDiscountChain(DocumentItem item)
    {
        var hasDiscountChain = item.PurchaseDiscount1 != 0m || item.PurchaseDiscount2 != 0m ||
            item.PurchaseDiscount3 != 0m || item.PurchaseDiscount4 != 0m;
        if (!hasDiscountChain)
        {
            return Round(item.UnitPrice * (1m - item.DiscountRate / 100m));
        }

        return Round(item.UnitPrice *
            (1m - item.PurchaseDiscount1 / 100m) *
            (1m - item.PurchaseDiscount2 / 100m) *
            (1m - item.PurchaseDiscount3 / 100m) *
            (1m - item.PurchaseDiscount4 / 100m));
    }
}
