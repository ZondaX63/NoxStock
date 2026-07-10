using System;
using Volo.Abp.Application.Dtos;

namespace NexStock.Documents;

public class DocumentItemDto : FullAuditedEntityDto<Guid>
{
    public Guid DocumentId { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; } // We will map this manually
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal Total { get; set; }
}
