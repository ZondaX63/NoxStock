namespace NoxStock.Documents;

public enum CommercialCommandType
{
    DocumentApproval = 1,
    PosSale = 2,
    DocumentCancellation = 3,
    PosCancellation = 4,
    PaymentAllocationBatch = 5,
    PaymentAllocationReversal = 6
}

public enum CommercialCommandStatus
{
    Processing = 1,
    Succeeded = 2,
    Failed = 3,
    Expired = 4
}
