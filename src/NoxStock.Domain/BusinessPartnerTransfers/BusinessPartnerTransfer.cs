using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NoxStock.BusinessPartnerTransfers;

/// <summary>
/// Records a customer's direct payment to a supplier on the tenant's behalf.
/// It is a non-cash settlement: customer receivable and supplier payable move together.
/// </summary>
public class BusinessPartnerTransfer : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    public Guid CustomerId { get; set; }
    public Guid SupplierId { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public DateTime TransferDate { get; set; }
    public string? Description { get; set; }

    protected BusinessPartnerTransfer() { }

    public BusinessPartnerTransfer(
        Guid id,
        Guid? tenantId,
        Guid customerId,
        Guid supplierId,
        string referenceNumber,
        decimal amount,
        string currency,
        DateTime transferDate,
        string? description)
        : base(id)
    {
        TenantId = tenantId;
        CustomerId = customerId;
        SupplierId = supplierId;
        ReferenceNumber = referenceNumber;
        Amount = amount;
        Currency = currency;
        TransferDate = transferDate;
        Description = description;
    }
}
