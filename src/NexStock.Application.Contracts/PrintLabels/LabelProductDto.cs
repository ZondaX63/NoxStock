using System;

namespace NexStock.PrintLabels;

public class LabelProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal SalePrice { get; set; }
    public string Currency { get; set; } = "TRY";
}
