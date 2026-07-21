using System;

namespace NoxStock.Products;

public class ProductPriceQuoteInputDto
{
    public Guid ProductId { get; set; }
    public string Currency { get; set; } = "TRY";
}

public class ProductPriceQuoteDto
{
    public Guid ProductId { get; set; }
    public bool IsAvailable { get; set; } = true;
    public string? UnavailableReason { get; set; }
    public string Currency { get; set; } = "TRY";
    public decimal UnitPrice { get; set; }
    public decimal PriceInTry { get; set; }
    public decimal ExchangeRate { get; set; } = 1;
    public DateTime? RateDate { get; set; }
    public bool UsesCurrentExchangeRate { get; set; }
    public decimal ProfitMarkupPercent { get; set; }
    public decimal PurchasePrice { get; set; }
    public string PurchaseCurrency { get; set; } = "TRY";
    public decimal SalePrice { get; set; }
    public string SaleCurrency { get; set; } = "TRY";
    public string PricingCurrencyCode { get; set; } = "TRY";
    public string? LastPurchaseCostCurrencyCode { get; set; }
    public decimal? LastPurchaseCostCurrencyAmount { get; set; }
    public decimal? LastPurchaseCostTryAmount { get; set; }
    public string? LastPurchaseSourceCurrencyCode { get; set; }
    public decimal? LastPurchaseSourceNetUnitCost { get; set; }
    public decimal? LastPurchaseSourceExchangeRate { get; set; }
    public decimal? PricingCurrencyExchangeRate { get; set; }
    public DateTime? PricingCurrencyRateDate { get; set; }
    public decimal? ReferenceSaleUnitPrice { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
    public string? LastPurchaseDocumentNumber { get; set; }
    public string? LastPurchaseSupplierName { get; set; }
    public Guid? ProductCostHistoryId { get; set; }
    public ProductPricingMode PricingMode { get; set; }
}
