using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NoxStock.TenantBackups;

public class TenantBackup : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    public TenantBackupStatus Status { get; private set; }
    public Guid? RequestedBy { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public long? FileSize { get; private set; }
    public string? ChecksumSha256 { get; private set; }
    public string? StorageName { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public string? FailureCode { get; private set; }

    protected TenantBackup() { }

    public TenantBackup(Guid id, Guid tenantId, Guid? requestedBy, DateTime requestedAtUtc, DateTime expiresAtUtc)
        : base(id)
    {
        TenantId = tenantId;
        RequestedBy = requestedBy;
        RequestedAtUtc = requestedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        Status = TenantBackupStatus.Queued;
    }

    public void MarkProcessing()
    {
        if (Status != TenantBackupStatus.Queued)
            throw new InvalidOperationException("Only a queued tenant backup can start processing.");
        Status = TenantBackupStatus.Processing;
    }

    public void MarkSucceeded(DateTime completedAtUtc, long fileSize, string checksumSha256, string storageName)
    {
        if (Status != TenantBackupStatus.Processing)
            throw new InvalidOperationException("Only a processing tenant backup can succeed.");
        Status = TenantBackupStatus.Succeeded;
        CompletedAtUtc = completedAtUtc;
        FileSize = fileSize;
        ChecksumSha256 = checksumSha256;
        StorageName = storageName;
        FailureCode = null;
    }

    public void MarkFailed(DateTime completedAtUtc, string failureCode)
    {
        if (Status is TenantBackupStatus.Succeeded or TenantBackupStatus.Failed)
            return;
        Status = TenantBackupStatus.Failed;
        CompletedAtUtc = completedAtUtc;
        FailureCode = failureCode;
        StorageName = null;
    }
}
