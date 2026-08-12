using System;
using System.ComponentModel.DataAnnotations;

namespace NoxStock.Products;

public class CreateUpdateProductDto
{
    public Guid? CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    
    [Required(ErrorMessage = "Ürün adı zorunludur.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(100)]
    public string Sku { get; set; } = string.Empty;
    
    [StringLength(100)]
    public string Barcode { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    [StringLength(50)]
    public string Unit { get; set; } = "Adet";
    
    [StringLength(100)]
    public string ShelfLocation { get; set; } = string.Empty;
    
    public decimal PurchasePrice { get; set; }
    public decimal PurchaseListPrice { get; set; }
    [Range(0, 100)] public decimal PurchaseDiscount1 { get; set; }
    [Range(0, 100)] public decimal PurchaseDiscount2 { get; set; }
    [Range(0, 100)] public decimal PurchaseDiscount3 { get; set; }
    [Range(0, 100)] public decimal PurchaseDiscount4 { get; set; }
    // TRY is normalized to 1 by the application service. Foreign currencies
    // start empty so omitted API/form values cannot silently become "1 TL".
    [Range(0, 1000000)] public decimal ExchangeRateAtPurchase { get; set; }
    public decimal SalePrice { get; set; }
    [Range(0, 100)] public decimal SalesDiscount1 { get; set; }
    [Range(0, 100)] public decimal SalesDiscount2 { get; set; }
    [Range(0, 100)] public decimal SalesDiscount3 { get; set; }
    [Range(0, 100)] public decimal SalesDiscount4 { get; set; }
    [Range(0, 100)] public int VatRate { get; set; } = 20;
    [Range(0, 10000)]
    public decimal ProfitMarkupPercent { get; set; } = 30m;
    public bool UseCurrentExchangeRateForSale { get; set; }
    public ProductPricingMode PricingMode { get; set; }
    public decimal Quantity { get; set; }
    public decimal CriticalStockLevel { get; set; }
    public bool TrackStock { get; set; } = true;
    
    [StringLength(3)]
    // A product's reference currency is independent from the purchase
    // document currency. EUR is the default reference for new cards; a TRY
    // purchase is converted with that document's EUR snapshot on approval.
    public string PurchaseCurrency { get; set; } = "EUR";

    [StringLength(1000)]
    public string? OemCodes { get; set; }
}
