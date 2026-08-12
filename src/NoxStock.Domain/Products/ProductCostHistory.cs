using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NoxStock.Products;

public class ProductCostHistory : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    public Guid ProductId { get; set; }
    public Guid SourceDocumentId { get; set; }
    public Guid SourceDocumentItemId { get; set; }
    public Guid? SupplierId { get; set; }
    public DateTime CostDate { get; set; }
    public string SourceCurrencyCode { get; set; } = "TRY";
    public decimal SourceNetUnitCost { get; set; }
    public decimal SourceExchangeRate { get; set; }
    public decimal TryEquivalentNetUnitCost { get; set; }
    public string ReferenceCurrencyCode { get; set; } = "TRY";
    public decimal ReferenceCurrencyEquivalentCost { get; set; }
    public decimal Quantity { get; set; }
    public decimal? QuantityBefore { get; set; }
    public decimal? AverageCostBeforeTry { get; set; }
    public decimal? QuantityAfter { get; set; }
    public decimal? AverageCostAfterTry { get; set; }
    public ProductCostHistoryChangeType ChangeType { get; set; }

    protected ProductCostHistory()
    {
    }

    public ProductCostHistory(Guid id, Guid? tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }
}
