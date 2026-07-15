using System;

namespace NexStock.Products;

public class ProductPriceQuoteInputDto
{
    public Guid ProductId { get; set; }
    public string Currency { get; set; } = "TRY";
}

public class ProductPriceQuoteDto
{
    public Guid ProductId { get; set; }
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
}
