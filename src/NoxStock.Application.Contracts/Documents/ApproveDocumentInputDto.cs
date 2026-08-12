using System;

namespace NoxStock.Documents;

public class ApproveDocumentInputDto
{
    public Guid? AccountId { get; set; }
    public decimal PaymentAmount { get; set; }
    public bool ConfirmBelowCost { get; set; }
}
