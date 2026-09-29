namespace NoxStock.TenantBackups;

public enum TenantCloneOperationStatus
{
    Queued = 1,
    Validating = 2,
    CreatingTenant = 3,
    ImportingMasterData = 4,
    ImportingCommercialData = 5,
    Verifying = 6,
    Succeeded = 7,
    Failed = 8,
    Cancelled = 9
}
