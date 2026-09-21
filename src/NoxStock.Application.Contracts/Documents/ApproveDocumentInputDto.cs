using System;

namespace NoxStock.Documents;

public class ApproveDocumentInputDto
{
    public string? OperationKey { get; set; }
    public Guid? AccountId { get; set; }
    public decimal PaymentAmount { get; set; }
    public bool ConfirmBelowCost { get; set; }
}

public class CancelDocumentInputDto
{
    public string? OperationKey { get; set; }
}
