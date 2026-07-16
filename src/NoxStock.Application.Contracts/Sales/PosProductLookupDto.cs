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
    public string Currency { get; set; } = "TRY";
    public decimal OriginalSalePrice { get; set; }
    public string SaleCurrency { get; set; } = "TRY";
    public decimal PurchasePrice { get; set; }
    public string PurchaseCurrency { get; set; } = "TRY";
    public bool UsesCurrentExchangeRate { get; set; }
    public decimal ProfitMarkupPercent { get; set; }
    public decimal ExchangeRate { get; set; } = 1;
    public decimal Quantity { get; set; }
    public bool TrackStock { get; set; }
}
