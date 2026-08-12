using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NoxStock.BusinessPartners;

public class BusinessPartnerOpeningBalance : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    public Guid BusinessPartnerId { get; set; }
    public BusinessPartnerOpeningDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public DateTime OpeningDate { get; set; }
    public string? Description { get; set; }
    public string OperationKey { get; set; } = string.Empty;
    public BusinessPartnerOpeningStatus Status { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }
    public Guid? ReversalOfOpeningBalanceId { get; set; }

    protected BusinessPartnerOpeningBalance()
    {
    }

    public BusinessPartnerOpeningBalance(
        Guid id,
        Guid? tenantId,
        Guid businessPartnerId,
        BusinessPartnerOpeningDirection direction,
        decimal amount,
        string currency,
        DateTime openingDate,
        string operationKey,
        string? description = null)
        : base(id)
    {
        TenantId = tenantId;
        BusinessPartnerId = businessPartnerId;
        Direction = direction;
        Amount = amount;
        Currency = currency;
        OpeningDate = openingDate;
        OperationKey = operationKey;
        Description = description;
        Status = BusinessPartnerOpeningStatus.Active;
    }
}
