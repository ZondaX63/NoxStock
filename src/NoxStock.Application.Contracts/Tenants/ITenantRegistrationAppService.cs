using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace NoxStock.Tenants;

public interface ITenantRegistrationAppService : IApplicationService
{
    Task RegisterTenantAsync(RegisterTenantDto input);
}
