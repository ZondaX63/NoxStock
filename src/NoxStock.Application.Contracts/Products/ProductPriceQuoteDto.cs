using System;

namespace NoxStock.Products;

public class ProductPriceQuoteInputDto
{
    public Guid ProductId { get; set; }
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
    public decimal PurchaseListPrice { get; set; }
    public decimal PurchaseDiscount1 { get; set; }
    public decimal PurchaseDiscount2 { get; set; }
    public decimal PurchaseDiscount3 { get; set; }
    public decimal PurchaseDiscount4 { get; set; }
    public decimal ExchangeRateAtPurchase { get; set; }
    public string PurchaseCurrency { get; set; } = "TRY";
    public decimal SalePrice { get; set; }
    public decimal SalesDiscount1 { get; set; }
    public decimal SalesDiscount2 { get; set; }
    public decimal SalesDiscount3 { get; set; }
    public decimal SalesDiscount4 { get; set; }
    public int VatRate { get; set; }
    public decimal NetCostInTry { get; set; }
    public decimal ListSalePriceInTry { get; set; }
    public decimal SalesDiscountAmountInTry { get; set; }
    public decimal VatAmountInTry { get; set; }
    public decimal CustomerFinalPriceInTry { get; set; }
    public decimal EstimatedProfitInTry { get; set; }
    public decimal EstimatedNetMarginPercent { get; set; }
    public decimal RealizedMarkupPercent { get; set; }
    public decimal GrossProfitMarginPercent { get; set; }
    public string ReferenceCurrencyCode { get; set; } = "TRY";
    public string? LastPurchaseCostCurrencyCode { get; set; }
    public decimal? LastPurchaseCostCurrencyAmount { get; set; }
    public decimal? LastPurchaseCostTryAmount { get; set; }
    public string? LastPurchaseSourceCurrencyCode { get; set; }
    public decimal? LastPurchaseSourceNetUnitCost { get; set; }
    public decimal? LastPurchaseSourceExchangeRate { get; set; }
    public decimal? ReferenceCurrencyExchangeRate { get; set; }
    public DateTime? ReferenceCurrencyRateDate { get; set; }
    public decimal? ReferenceSaleUnitPrice { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
    public string? LastPurchaseDocumentNumber { get; set; }
    public string? LastPurchaseSupplierName { get; set; }
    public Guid? ProductCostHistoryId { get; set; }
    public ProductPricingMode PricingMode { get; set; }
}
