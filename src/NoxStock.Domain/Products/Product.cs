using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NoxStock.Products;

public class Product : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    
    public Guid? CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    
    public string Name { get; set; }
    public string Sku { get; set; }
    public string Barcode { get; set; }
    public string Description { get; set; }
    public string Unit { get; set; }
    public string ShelfLocation { get; set; }
    
    public decimal PurchasePrice { get; set; }
    public decimal PurchaseListPrice { get; set; }
    public decimal PurchaseDiscount1 { get; set; }
    public decimal PurchaseDiscount2 { get; set; }
    public decimal PurchaseDiscount3 { get; set; }
    public decimal PurchaseDiscount4 { get; set; }
    public decimal ExchangeRateAtPurchase { get; set; } = 1m;
    public decimal SalePrice { get; set; }
    public decimal SalesDiscount1 { get; set; }
    public decimal SalesDiscount2 { get; set; }
    public decimal SalesDiscount3 { get; set; }
    public decimal SalesDiscount4 { get; set; }
    public int VatRate { get; set; } = 20;
    public decimal ProfitMarkupPercent { get; set; } = 30m;
    public bool UseCurrentExchangeRateForSale { get; set; }
    public ProductPricingMode PricingMode { get; set; }
    public string? LastPurchaseCostCurrencyCode { get; set; }
    public decimal? LastPurchaseCostCurrencyAmount { get; set; }
    public decimal? LastPurchaseCostTryAmount { get; set; }
    public decimal? LastPurchaseExchangeRate { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
    public Guid? LastPurchaseDocumentId { get; set; }
    public Guid? LastPurchaseDocumentItemId { get; set; }
    public Guid? LastPurchaseCostHistoryId { get; set; }
    public Guid? LastPurchaseSupplierId { get; set; }
    public DateTime? LastCostUpdatedAt { get; set; }
    public decimal Quantity { get; set; }
    public decimal CriticalStockLevel { get; set; }
    public bool TrackStock { get; set; }
    
    public string PurchaseCurrency { get; set; } = "EUR";

    public ICollection<ProductOEMCode> OemCodes { get; set; }

    public Product()
    {
        OemCodes = new List<ProductOEMCode>();
    }

    public Product(Guid id, Guid? tenantId, string name, string sku) : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Sku = sku;
        OemCodes = new List<ProductOEMCode>();
    }
}
