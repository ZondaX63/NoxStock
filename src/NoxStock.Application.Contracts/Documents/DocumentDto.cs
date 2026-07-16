using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Documents;

public class DocumentDto : FullAuditedEntityDto<Guid>
{
    public Guid BusinessPartnerId { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public string? BusinessPartnerName { get; set; }
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

    public List<DocumentItemDto> Items { get; set; }

    public DocumentDto()
    {
        Items = new List<DocumentItemDto>();
    }
}
