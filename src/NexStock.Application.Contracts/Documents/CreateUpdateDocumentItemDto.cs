using System;

namespace NexStock.Documents;

public class CreateUpdateDocumentItemDto
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal Total { get; set; }
}
