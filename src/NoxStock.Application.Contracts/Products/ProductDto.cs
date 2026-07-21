using System;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Products;

public class ProductDto : FullAuditedEntityDto<Guid>
{
    public Guid? CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public string Name { get; set; }
    public string Sku { get; set; }
    public string Barcode { get; set; }
    public string Description { get; set; }
    public string Unit { get; set; }
    public string ShelfLocation { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal ProfitMarkupPercent { get; set; }
    public bool UseCurrentExchangeRateForSale { get; set; }
    public string PricingCurrencyCode { get; set; } = "TRY";
    public ProductPricingMode PricingMode { get; set; }
    public string? LastPurchaseCostCurrencyCode { get; set; }
    public decimal? LastPurchaseCostCurrencyAmount { get; set; }
    public decimal? LastPurchaseCostTryAmount { get; set; }
    public string? LastPurchaseSourceCurrencyCode { get; set; }
    public decimal? LastPurchaseSourceNetUnitCost { get; set; }
    public decimal? LastPurchaseSourceExchangeRate { get; set; }
    public decimal? LastPurchaseExchangeRate { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
    public Guid? LastPurchaseDocumentId { get; set; }
    public Guid? LastPurchaseDocumentItemId { get; set; }
    public Guid? LastPurchaseCostHistoryId { get; set; }
    public Guid? LastPurchaseSupplierId { get; set; }
    public DateTime? LastCostUpdatedAt { get; set; }
    public decimal Quantity { get; set; }
    public decimal CriticalStockLevel { get; set; }
    public bool TrackStock { get; set; }
    public string Currency { get; set; }
    public string PurchaseCurrency { get; set; }
    public string SaleCurrency { get; set; }
    public string? OemCodes { get; set; }
    
    // Derived properties for UI
    public string BrandName { get; set; }
    public string CategoryName { get; set; }
    public string? LastPurchaseDocumentNumber { get; set; }
    public string? LastPurchaseSupplierName { get; set; }

    // Server-calculated list preview. A list must never recalculate dynamic prices in the browser.
    public bool IsSuggestedSaleAvailable { get; set; } = true;
    public string? SuggestedSaleUnavailableReason { get; set; }
    public string SuggestedSaleCurrency { get; set; } = "TRY";
    public decimal? SuggestedSaleUnitPrice { get; set; }
    public decimal? SuggestedSalePriceInTry { get; set; }
    public decimal? SuggestedSaleExchangeRate { get; set; }
    public DateTime? SuggestedSaleRateDate { get; set; }
    public decimal? CurrentPricingCurrencyExchangeRate { get; set; }
    public DateTime? CurrentPricingCurrencyRateDate { get; set; }
    public decimal? ReferenceSaleUnitPrice { get; set; }
}
