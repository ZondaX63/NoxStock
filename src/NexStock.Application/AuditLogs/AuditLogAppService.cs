using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NexStock.AuditLogs;
using NexStock.Permissions;
using NexStock.MultiTenancy;
using Volo.Abp.Application.Services;
using Volo.Abp.AuditLogging;

namespace NexStock.AuditLogs;

[Authorize(NexStockPermissions.AuditLogs.Default)]
public class AuditLogAppService : ApplicationService, IAuditLogAppService
{
    private readonly IAuditLogRepository _auditLogRepository;

    public AuditLogAppService(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task<AuditLogSnapshotDto> GetListAsync(AuditLogRequestDto input)
    {
        CurrentTenant.RequireHost();
        var dateFrom = input.DateFrom?.Date;
        var dateTo = input.DateTo?.Date.AddDays(1);
        var maxResultCount = Math.Clamp(input.MaxResultCount, 1, 100);
        var skipCount = Math.Max(0, input.SkipCount);

        var totalCount = await _auditLogRepository.GetCountAsync(
            startTime: dateFrom,
            endTime: dateTo,
            userName: string.IsNullOrWhiteSpace(input.UserName) ? null : input.UserName.Trim(),
            httpMethod: string.IsNullOrWhiteSpace(input.HttpMethod) ? null : input.HttpMethod.Trim().ToUpperInvariant(),
            hasException: input.HasException);

        var logs = await _auditLogRepository.GetListAsync(
            sorting: "ExecutionTime desc",
            maxResultCount: maxResultCount,
            skipCount: skipCount,
            startTime: dateFrom,
            endTime: dateTo,
            userName: string.IsNullOrWhiteSpace(input.UserName) ? null : input.UserName.Trim(),
            httpMethod: string.IsNullOrWhiteSpace(input.HttpMethod) ? null : input.HttpMethod.Trim().ToUpperInvariant(),
            hasException: input.HasException,
            includeDetails: true);

        return new AuditLogSnapshotDto
        {
            TotalCount = totalCount,
            Items = logs.Select(log => new AuditLogItemDto
            {
                Id = log.Id,
                ExecutionTime = log.ExecutionTime,
                UserName = log.UserName ?? "Sistem",
                HttpMethod = log.HttpMethod ?? string.Empty,
                Url = log.Url ?? string.Empty,
                ClientIpAddress = log.ClientIpAddress ?? string.Empty,
                ExecutionDuration = log.ExecutionDuration,
                HttpStatusCode = log.HttpStatusCode,
                HasException = !string.IsNullOrWhiteSpace(log.Exceptions),
                EntityChangeCount = log.EntityChanges?.Count ?? 0,
                ServiceName = log.Actions?.OrderBy(x => x.ExecutionTime).FirstOrDefault()?.ServiceName ?? string.Empty,
                MethodName = log.Actions?.OrderBy(x => x.ExecutionTime).FirstOrDefault()?.MethodName ?? string.Empty
            }).ToList()
        };
    }
}
