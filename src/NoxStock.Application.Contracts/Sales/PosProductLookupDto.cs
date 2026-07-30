using System;

namespace NoxStock.Sales;

public class PosProductLookupInputDto
{
    public string? Filter { get; set; }
    public int MaxResultCount { get; set; } = 30;
}

public class PosProductLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string? OemCodes { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal SalePrice { get; set; }
    public decimal ListSalePrice { get; set; }
    public decimal NetSalePrice { get; set; }
    public decimal CustomerFinalUnitPrice { get; set; }
    public decimal VatRate { get; set; }
    public decimal SalesDiscount1 { get; set; }
    public decimal SalesDiscount2 { get; set; }
    public decimal SalesDiscount3 { get; set; }
    public decimal SalesDiscount4 { get; set; }
    public string Currency { get; set; } = "TRY";
    public decimal OriginalSalePrice { get; set; }
    public decimal PurchasePrice { get; set; }
    public string PurchaseCurrency { get; set; } = "TRY";
    public bool UsesCurrentExchangeRate { get; set; }
    public decimal ProfitMarkupPercent { get; set; }
    public decimal ExchangeRate { get; set; } = 1;
    public string? LastPurchaseCostCurrencyCode { get; set; }
    public decimal? LastPurchaseCostCurrencyAmount { get; set; }
    public decimal? LastPurchaseCostTryAmount { get; set; }
    public string? LastPurchaseSourceCurrencyCode { get; set; }
    public decimal? LastPurchaseSourceNetUnitCost { get; set; }
    public decimal? LastPurchaseSourceExchangeRate { get; set; }
    public string ReferenceCurrencyCode { get; set; } = "TRY";
    public decimal? ReferenceCurrencyExchangeRate { get; set; }
    public DateTime? ReferenceCurrencyRateDate { get; set; }
    public decimal? ReferenceSaleUnitPrice { get; set; }
    public bool IsPriceAvailable { get; set; } = true;
    public string? PriceUnavailableReason { get; set; }
    public Guid? ProductCostHistoryId { get; set; }
    public decimal Quantity { get; set; }
    public bool TrackStock { get; set; }
}
