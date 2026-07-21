using System;
using NoxStock.Products;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NoxStock.Documents;

public class DocumentItem : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    
    public Guid DocumentId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal Total { get; set; }
    public bool IsUnitPriceOverridden { get; set; }
    public string? PriceCurrencyCode { get; set; }
    public decimal? PriceExchangeRate { get; set; }
    public DateTime? PriceRateDate { get; set; }
    public decimal? UnitPriceTry { get; set; }
    public Guid? ProductCostHistoryId { get; set; }
    public ProductPricingMode? PricingMode { get; set; }
    public string? CostCurrencyCode { get; set; }
    public decimal? CostCurrencyAmount { get; set; }
    public decimal? CostTryAmount { get; set; }
    public decimal? AppliedMarkupPercent { get; set; }
    public string? ReferenceSaleCurrencyCode { get; set; }
    public decimal? ReferenceSaleUnitPrice { get; set; }
    public decimal? ReferenceSaleExchangeRate { get; set; }
    public DateTime? PricingSnapshotDate { get; set; }

    public DocumentItem()
    {
    }

    public DocumentItem(Guid id, Guid? tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }
}
