using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace NoxStock.Reports;

public interface IReportingAppService : IApplicationService
{
    Task<ReportSnapshotDto> GetSnapshotAsync(ReportRequestDto input);
}
