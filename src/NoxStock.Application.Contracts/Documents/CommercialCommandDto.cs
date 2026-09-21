using System;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Documents;

public class CommercialCommandDto : EntityDto<Guid>
{
    public CommercialCommandType Type { get; set; }
    public string OperationKey { get; set; } = string.Empty;
    public Guid? DocumentId { get; set; }
    public CommercialCommandStatus Status { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? FailureCode { get; set; }
}
