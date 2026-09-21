using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NoxStock.Documents;

public class PaymentAllocation : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    public Guid AccountTransactionId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public DateTime AllocationDate { get; set; }
    public PaymentAllocationStatus Status { get; set; } = PaymentAllocationStatus.Active;
    public DateTime? ReversedAt { get; set; }
    public Guid? ReversedBy { get; set; }

    protected PaymentAllocation()
    {
    }

    public PaymentAllocation(
        Guid id,
        Guid? tenantId,
        Guid accountTransactionId,
        Guid documentId,
        Guid businessPartnerId,
        decimal amount,
        string currency,
        DateTime allocationDate)
        : base(id)
    {
        TenantId = tenantId;
        AccountTransactionId = accountTransactionId;
        DocumentId = documentId;
        BusinessPartnerId = businessPartnerId;
        Amount = amount;
        Currency = currency;
        AllocationDate = allocationDate;
    }
}
