using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace NexStock.Reports;

public interface IReportingAppService : IApplicationService
{
    Task<ReportSnapshotDto> GetSnapshotAsync(ReportRequestDto input);
}
