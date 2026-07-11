using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NexStock.Documents;

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

    public ICollection<DocumentItem> Items { get; set; }

    public Document()
    {
        Items = new List<DocumentItem>();
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
        Currency = "TRY";
        ExchangeRate = 1;
        Date = DateTime.UtcNow;
    }
}
