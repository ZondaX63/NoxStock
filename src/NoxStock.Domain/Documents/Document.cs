using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NoxStock.Documents;

public class Document : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    
    public Guid BusinessPartnerId { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public string DocNumber { get; set; }
    public DocumentType Type { get; set; }
    public DocumentStatus Status { get; set; }
    public decimal Subtotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public string Currency { get; set; }
    public decimal ExchangeRate { get; set; }
    public DateTime Date { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Description { get; set; }
    public string? OperationKey { get; set; }

    public ICollection<DocumentItem> Items { get; set; }
    public ICollection<DocumentExchangeRateSnapshot> ExchangeRateSnapshots { get; set; }

    public Document()
    {
        Items = new List<DocumentItem>();
        ExchangeRateSnapshots = new List<DocumentExchangeRateSnapshot>();
        DocNumber = string.Empty;
        Currency = "TRY";
        ExchangeRate = 1;
        Date = DateTime.UtcNow;
    }

    public Document(Guid id, Guid? tenantId)
        : base(id)
    {
        TenantId = tenantId;
        DocNumber = string.Empty;
        Items = new List<DocumentItem>();
        ExchangeRateSnapshots = new List<DocumentExchangeRateSnapshot>();
        Currency = "TRY";
        ExchangeRate = 1;
        Date = DateTime.UtcNow;
    }

    public void TransitionTo(DocumentStatus targetStatus)
    {
        if (Status == targetStatus)
        {
            return;
        }

        var isAllowed = Status switch
        {
            DocumentStatus.Draft => targetStatus is DocumentStatus.Sent or DocumentStatus.Approved or
                DocumentStatus.PartiallyPaid or DocumentStatus.Paid or DocumentStatus.Completed,
            DocumentStatus.Sent => targetStatus is DocumentStatus.Approved or DocumentStatus.PartiallyPaid or
                DocumentStatus.Paid or DocumentStatus.Completed,
            DocumentStatus.Approved => targetStatus is DocumentStatus.PartiallyPaid or DocumentStatus.Paid or
                DocumentStatus.Cancelled,
            DocumentStatus.PartiallyPaid => targetStatus is DocumentStatus.Paid or DocumentStatus.Cancelled,
            DocumentStatus.Paid => targetStatus == DocumentStatus.Cancelled,
            DocumentStatus.Completed => targetStatus == DocumentStatus.Cancelled,
            _ => false
        };

        if (!isAllowed)
        {
            throw new BusinessException("NoxStock:InvalidDocumentStatusTransition")
                .WithData("CurrentStatus", Status)
                .WithData("TargetStatus", targetStatus);
        }

        Status = targetStatus;
    }
}
