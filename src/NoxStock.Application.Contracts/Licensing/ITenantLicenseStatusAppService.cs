using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace NoxStock.Licensing;

public interface ITenantLicenseStatusAppService : IApplicationService
{
    Task<TenantLicenseAccessDto> GetCurrentAsync();
}
