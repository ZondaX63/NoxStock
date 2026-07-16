using System;

namespace NoxStock.Documents;

public class RecordDocumentPaymentInputDto
{
    public string? OperationKey { get; set; }
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? Description { get; set; }
}
