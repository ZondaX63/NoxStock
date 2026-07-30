using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using NoxStock.MultiTenancy;
using NoxStock.Permissions;
using NoxStock.BusinessPartners;
using NoxStock.Documents;

namespace NoxStock.Products;

[Authorize(NoxStockPermissions.Products.Default)]
public class ProductPricingAppService : ApplicationService, IProductPricingAppService
{
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<ProductCostHistory, Guid> _costHistoryRepository;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IProductPriceCalculator _priceCalculator;

    public ProductPricingAppService(
        IRepository<Product, Guid> productRepository,
        IProductPriceCalculator priceCalculator,
        IRepository<ProductCostHistory, Guid> costHistoryRepository,
        IRepository<Document, Guid> documentRepository,
        IRepository<BusinessPartner, Guid> businessPartnerRepository)
    {
        _productRepository = productRepository;
        _priceCalculator = priceCalculator;
        _costHistoryRepository = costHistoryRepository;
        _documentRepository = documentRepository;
        _businessPartnerRepository = businessPartnerRepository;
    }

    public async Task<ProductPriceQuoteDto> GetQuoteAsync(ProductPriceQuoteInputDto input)
    {
        CurrentTenant.RequireTenantId();
        var product = await _productRepository.GetAsync(input.ProductId);
        var calculation = await _priceCalculator.CalculateAsync(product);

        var quote = new ProductPriceQuoteDto
        {
            ProductId = product.Id,
            IsAvailable = calculation.IsAvailable,
            UnavailableReason = calculation.UnavailableReason,
            Currency = calculation.Currency,
            UnitPrice = calculation.UnitPrice,
            PriceInTry = calculation.PriceInTry,
            ExchangeRate = calculation.ExchangeRate,
            RateDate = calculation.RateDate,
            UsesCurrentExchangeRate = calculation.UsesCurrentExchangeRate,
            ProfitMarkupPercent = product.ProfitMarkupPercent,
            PurchasePrice = product.PurchasePrice,
            PurchaseListPrice = product.PurchaseListPrice,
            PurchaseDiscount1 = product.PurchaseDiscount1,
            PurchaseDiscount2 = product.PurchaseDiscount2,
            PurchaseDiscount3 = product.PurchaseDiscount3,
            PurchaseDiscount4 = product.PurchaseDiscount4,
            ExchangeRateAtPurchase = product.ExchangeRateAtPurchase,
            PurchaseCurrency = product.PurchaseCurrency,
            SalePrice = product.SalePrice,
            SalesDiscount1 = product.SalesDiscount1,
            SalesDiscount2 = product.SalesDiscount2,
            SalesDiscount3 = product.SalesDiscount3,
            SalesDiscount4 = product.SalesDiscount4,
            VatRate = product.VatRate,
            NetCostInTry = calculation.NetCostInTry,
            ListSalePriceInTry = calculation.ListSalePriceInTry,
            SalesDiscountAmountInTry = calculation.SalesDiscountAmountInTry,
            VatAmountInTry = calculation.VatAmountInTry,
            CustomerFinalPriceInTry = calculation.CustomerFinalPriceInTry,
            EstimatedProfitInTry = calculation.EstimatedProfitInTry,
            EstimatedNetMarginPercent = calculation.EstimatedNetMarginPercent,
            RealizedMarkupPercent = calculation.RealizedMarkupPercent,
            GrossProfitMarginPercent = calculation.GrossProfitMarginPercent,
            ReferenceCurrencyCode = product.PurchaseCurrency,
            LastPurchaseCostCurrencyCode = product.LastPurchaseCostCurrencyCode,
            LastPurchaseCostCurrencyAmount = product.LastPurchaseCostCurrencyAmount,
            LastPurchaseCostTryAmount = product.LastPurchaseCostTryAmount,
            ReferenceCurrencyExchangeRate = calculation.ReferenceCurrencyExchangeRate,
            ReferenceCurrencyRateDate = calculation.ReferenceCurrencyRateDate,
            ReferenceSaleUnitPrice = calculation.ReferenceSaleUnitPrice,
            LastPurchaseDate = product.LastPurchaseDate,
            ProductCostHistoryId = calculation.ProductCostHistoryId,
            PricingMode = product.PricingMode
        };

        if (product.LastPurchaseCostHistoryId.HasValue)
        {
            var history = await _costHistoryRepository.FindAsync(product.LastPurchaseCostHistoryId.Value);
            if (history != null)
            {
                quote.LastPurchaseSourceCurrencyCode = history.SourceCurrencyCode;
                quote.LastPurchaseSourceNetUnitCost = history.SourceNetUnitCost;
                quote.LastPurchaseSourceExchangeRate = history.SourceExchangeRate;
            }
        }

        if (product.LastPurchaseDocumentId.HasValue)
        {
            quote.LastPurchaseDocumentNumber = (await _documentRepository.FindAsync(product.LastPurchaseDocumentId.Value))?.DocNumber;
        }

        if (product.LastPurchaseSupplierId.HasValue)
        {
            quote.LastPurchaseSupplierName = (await _businessPartnerRepository.FindAsync(product.LastPurchaseSupplierId.Value))?.Name;
        }

        return quote;
    }

    public async Task<List<ProductCostHistoryDto>> GetCostHistoryAsync(Guid productId)
    {
        CurrentTenant.RequireTenantId();
        await _productRepository.GetAsync(productId);

        var historyQuery = await _costHistoryRepository.GetQueryableAsync();
        var histories = await AsyncExecuter.ToListAsync(historyQuery
            .Where(x => x.ProductId == productId)
            .OrderByDescending(x => x.CostDate)
            .ThenByDescending(x => x.CreationTime)
            .Take(50));

        var documentIds = histories.Select(x => x.SourceDocumentId).Distinct().ToList();
        var supplierIds = histories.Where(x => x.SupplierId.HasValue).Select(x => x.SupplierId!.Value).Distinct().ToList();
        var documentQuery = await _documentRepository.GetQueryableAsync();
        var supplierQuery = await _businessPartnerRepository.GetQueryableAsync();
        var documentNumbers = (await AsyncExecuter.ToListAsync(documentQuery.Where(x => documentIds.Contains(x.Id))))
            .ToDictionary(x => x.Id, x => x.DocNumber);
        var supplierNames = (await AsyncExecuter.ToListAsync(supplierQuery.Where(x => supplierIds.Contains(x.Id))))
            .ToDictionary(x => x.Id, x => x.Name);

        return histories.Select(history => new ProductCostHistoryDto
        {
            CostDate = history.CostDate,
            SupplierName = history.SupplierId.HasValue ? supplierNames.GetValueOrDefault(history.SupplierId.Value) : null,
            SourceDocumentNumber = documentNumbers.GetValueOrDefault(history.SourceDocumentId),
            SourceNetUnitCost = history.SourceNetUnitCost,
            SourceCurrencyCode = history.SourceCurrencyCode,
            SourceExchangeRate = history.SourceExchangeRate,
            TryEquivalentNetUnitCost = history.TryEquivalentNetUnitCost,
            ReferenceCurrencyCode = history.ReferenceCurrencyCode,
            ReferenceCurrencyEquivalentCost = history.ReferenceCurrencyEquivalentCost,
            ChangeType = (int)history.ChangeType
        }).ToList();
    }
}
