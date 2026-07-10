using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NexStock.Documents;

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

    public DocumentItem()
    {
    }

    public DocumentItem(Guid id, Guid? tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }
}
