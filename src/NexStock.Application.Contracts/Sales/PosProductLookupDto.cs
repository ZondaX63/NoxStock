using System;

namespace NexStock.Sales;

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
    public string Unit { get; set; } = string.Empty;
    public decimal SalePrice { get; set; }
    public string Currency { get; set; } = "TRY";
    public decimal Quantity { get; set; }
    public bool TrackStock { get; set; }
}
