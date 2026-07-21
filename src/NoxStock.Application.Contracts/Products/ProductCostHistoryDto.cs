using System;

namespace NoxStock.Products;

public class ProductCostHistoryDto
{
    public DateTime CostDate { get; set; }
    public string? SupplierName { get; set; }
    public string? SourceDocumentNumber { get; set; }
    public decimal SourceNetUnitCost { get; set; }
    public string SourceCurrencyCode { get; set; } = "TRY";
    public decimal SourceExchangeRate { get; set; }
    public decimal TryEquivalentNetUnitCost { get; set; }
    public string PricingCurrencyCode { get; set; } = "TRY";
    public decimal PricingCurrencyEquivalentCost { get; set; }
    public int ChangeType { get; set; }
}
