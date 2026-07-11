using System;
using System.Collections.Generic;

namespace NexStock.AuditLogs;

public class AuditLogRequestDto
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? UserName { get; set; }
    public string? HttpMethod { get; set; }
    public bool? HasException { get; set; }
    public int SkipCount { get; set; }
    public int MaxResultCount { get; set; } = 50;
}

public class AuditLogItemDto
{
    public Guid Id { get; set; }
    public DateTime ExecutionTime { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public string ClientIpAddress { get; set; } = string.Empty;
    public int ExecutionDuration { get; set; }
    public int? HttpStatusCode { get; set; }
    public bool HasException { get; set; }
    public int EntityChangeCount { get; set; }
}

public class AuditLogSnapshotDto
{
    public long TotalCount { get; set; }
    public List<AuditLogItemDto> Items { get; set; } = new();
}

public interface IAuditLogAppService : Volo.Abp.Application.Services.IApplicationService
{
    System.Threading.Tasks.Task<AuditLogSnapshotDto> GetListAsync(AuditLogRequestDto input);
}
