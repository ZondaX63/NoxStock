using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace NoxStock.Settings;

public interface ITenantOperationalDataResetAppService : IApplicationService
{
    Task<TenantOperationalDataResetResultDto> ResetAsync(TenantOperationalDataResetInputDto input);
}
