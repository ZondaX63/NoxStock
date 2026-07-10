using System;

namespace NexStock.Documents;

public class ApproveDocumentInputDto
{
    public Guid? AccountId { get; set; }
    public decimal PaymentAmount { get; set; }
}

