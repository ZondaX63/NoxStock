using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace NoxStock.Licensing;

public interface IHostTenantUserAppService : IApplicationService
{
    Task<HostTenantUserDto> CreateAsync(CreateHostTenantUserDto input);
}
