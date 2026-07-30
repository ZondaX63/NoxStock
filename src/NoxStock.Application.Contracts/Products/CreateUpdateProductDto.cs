using System;
using System.ComponentModel.DataAnnotations;

namespace NoxStock.Products;

public class CreateUpdateProductDto
{
    public Guid? CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    
    [Required]
    [StringLength(150)]
    public string Name { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Sku { get; set; }
    
    [StringLength(100)]
    public string Barcode { get; set; }
    
    public string Description { get; set; }
    
    [StringLength(50)]
    public string Unit { get; set; } = "Adet";
    
    [StringLength(100)]
    public string ShelfLocation { get; set; }
    
    public decimal PurchasePrice { get; set; }
    public decimal PurchaseListPrice { get; set; }
    [Range(0, 100)] public decimal PurchaseDiscount1 { get; set; }
    [Range(0, 100)] public decimal PurchaseDiscount2 { get; set; }
    [Range(0, 100)] public decimal PurchaseDiscount3 { get; set; }
    [Range(0, 100)] public decimal PurchaseDiscount4 { get; set; }
    [Range(0, 1000000)] public decimal ExchangeRateAtPurchase { get; set; } = 1m;
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
    public string PurchaseCurrency { get; set; } = "TRY";

    [StringLength(1000)]
    public string? OemCodes { get; set; }
}
