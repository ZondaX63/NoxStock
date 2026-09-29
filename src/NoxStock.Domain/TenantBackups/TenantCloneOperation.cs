using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace NoxStock.TenantBackups;

// Host-owned: it deliberately has no TenantId because it coordinates creation
// of a new tenant and must never be visible in an ordinary tenant context.
public class TenantCloneOperation : FullAuditedAggregateRoot<Guid>
{
    public Guid SourceBackupId { get; private set; }
    public string TargetTenantName { get; private set; } = string.Empty;
    public string TargetTenantCode { get; private set; } = string.Empty;
    public string? OperationKey { get; private set; }
    public TenantCloneOperationStatus Status { get; private set; }
    public long ProcessedRecordCount { get; private set; }
    public Guid? TargetTenantId { get; private set; }
    public string? FailureCode { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string ProtectedPackagePassword { get; private set; } = string.Empty;
    public string ProtectedAdminPassword { get; private set; } = string.Empty;
    public string AdminEmailAddress { get; private set; } = string.Empty;

    protected TenantCloneOperation() { }

    public TenantCloneOperation(Guid id, Guid sourceBackupId, string targetTenantName, string targetTenantCode,
        string? operationKey, string protectedPackagePassword, string adminEmailAddress, string protectedAdminPassword,
        DateTime requestedAtUtc)
        : base(id)
    {
        SourceBackupId = sourceBackupId;
        TargetTenantName = targetTenantName;
        TargetTenantCode = targetTenantCode;
        OperationKey = operationKey;
        ProtectedPackagePassword = protectedPackagePassword;
        AdminEmailAddress = adminEmailAddress;
        ProtectedAdminPassword = protectedAdminPassword;
        RequestedAtUtc = requestedAtUtc;
        Status = TenantCloneOperationStatus.Queued;
    }

    public void MarkProgress(TenantCloneOperationStatus status, long processedRecordCount)
    {
        if (Status is TenantCloneOperationStatus.Succeeded or TenantCloneOperationStatus.Failed or TenantCloneOperationStatus.Cancelled)
            throw new InvalidOperationException("A terminal clone operation cannot change state.");
        if (status is TenantCloneOperationStatus.Queued or TenantCloneOperationStatus.Succeeded or TenantCloneOperationStatus.Failed or TenantCloneOperationStatus.Cancelled)
            throw new InvalidOperationException("The requested clone state is not an active progress state.");
        if ((int)status < (int)Status)
            throw new InvalidOperationException("Clone progress cannot move backwards.");
        Status = status;
        ProcessedRecordCount = processedRecordCount;
    }

    public void MarkSucceeded(Guid targetTenantId, long processedRecordCount, DateTime completedAtUtc)
    {
        if (Status is TenantCloneOperationStatus.Succeeded or TenantCloneOperationStatus.Failed or TenantCloneOperationStatus.Cancelled)
            throw new InvalidOperationException("A terminal clone operation cannot change state.");
        TargetTenantId = targetTenantId;
        ProcessedRecordCount = processedRecordCount;
        CompletedAtUtc = completedAtUtc;
        FailureCode = null;
        Status = TenantCloneOperationStatus.Succeeded;
        ClearProtectedCredentials();
    }

    public void MarkFailed(string failureCode, DateTime completedAtUtc)
    {
        if (Status is TenantCloneOperationStatus.Succeeded or TenantCloneOperationStatus.Failed or TenantCloneOperationStatus.Cancelled)
            return;
        FailureCode = failureCode;
        CompletedAtUtc = completedAtUtc;
        Status = TenantCloneOperationStatus.Failed;
        ClearProtectedCredentials();
    }

    public void Cancel(DateTime completedAtUtc)
    {
        if (Status != TenantCloneOperationStatus.Queued)
            throw new InvalidOperationException("Only a queued clone operation can be cancelled.");
        CompletedAtUtc = completedAtUtc;
        Status = TenantCloneOperationStatus.Cancelled;
        ClearProtectedCredentials();
    }

    private void ClearProtectedCredentials()
    {
        ProtectedPackagePassword = string.Empty;
        ProtectedAdminPassword = string.Empty;
    }
}
