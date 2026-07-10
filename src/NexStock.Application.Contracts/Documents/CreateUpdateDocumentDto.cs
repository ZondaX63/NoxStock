using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NexStock.Documents;

public class CreateUpdateDocumentDto
{
    [Required]
    public Guid BusinessPartnerId { get; set; }
    
    [Required]
    [StringLength(100)]
    public string DocNumber { get; set; }
    
    [Required]
    public DocumentType Type { get; set; }
    
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    
    public decimal Subtotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    
    [StringLength(3)]
    public string Currency { get; set; } = "TRY";
    
    public decimal ExchangeRate { get; set; } = 1;
    
    [Required]
    public DateTime Date { get; set; } = DateTime.UtcNow;
    
    public DateTime? DueDate { get; set; }
    
    public string? Description { get; set; }

    public List<CreateUpdateDocumentItemDto> Items { get; set; }

    public CreateUpdateDocumentDto()
    {
        Items = new List<CreateUpdateDocumentItemDto>();
    }
}
