using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace NoxStock.Dashboard;

public interface IDashboardAppService : IApplicationService
{
    Task<DashboardSummaryDto> GetSummaryAsync();
}

