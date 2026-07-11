using System;

namespace NexStock.Documents;

public class RecordDocumentPaymentInputDto
{
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? Description { get; set; }
}
