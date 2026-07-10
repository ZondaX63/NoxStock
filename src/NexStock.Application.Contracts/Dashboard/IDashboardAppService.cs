using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace NexStock.Dashboard;

public interface IDashboardAppService : IApplicationService
{
    Task<DashboardSummaryDto> GetSummaryAsync();
}

