using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NoxStock.Documents;

public class CommercialCommand : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    public CommercialCommandType Type { get; set; }
    public string OperationKey { get; set; } = string.Empty;
    public Guid? DocumentId { get; set; }
    public CommercialCommandStatus Status { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public Guid? ExecutionToken { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? FailureCode { get; set; }

    public CommercialCommand(Guid id, Guid? tenantId, CommercialCommandType type, string operationKey)
        : base(id)
    {
        TenantId = tenantId;
        Type = type;
        OperationKey = operationKey;
        Status = CommercialCommandStatus.Processing;
        ProcessingStartedAt = DateTime.UtcNow;
        LeaseExpiresAt = ProcessingStartedAt.Value.AddMinutes(5);
        ExecutionToken = Guid.NewGuid();
        AttemptCount = 1;
    }

    protected CommercialCommand() { }
}
