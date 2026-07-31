using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Notifications;

public class OperationalAlertDto
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}

public class OperationalAlertsSummaryDto
{
    public int TotalCount { get; set; }
    public int CriticalCount { get; set; }
    public int WarningCount { get; set; }
    public List<OperationalAlertDto> Alerts { get; set; } = new();
}

public class OperationalAlertGetListInput : PagedResultRequestDto
{
}

public class OperationalAlertsPageDto
{
    public int TotalCount { get; set; }
    public int CriticalCount { get; set; }
    public int WarningCount { get; set; }
    public int SkipCount { get; set; }
    public int MaxResultCount { get; set; }
    public List<OperationalAlertDto> Alerts { get; set; } = new();
}

public interface IOperationalAlertsAppService : Volo.Abp.Application.Services.IApplicationService
{
    System.Threading.Tasks.Task<OperationalAlertsSummaryDto> GetSummaryAsync();
    System.Threading.Tasks.Task<OperationalAlertsPageDto> GetPageAsync(OperationalAlertGetListInput input);
}
