using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace NoxStock.Settings;

public interface ICompanySettingsAppService : IApplicationService
{
    Task<CompanySettingsDto> GetAsync();
    Task<CompanySettingsDto> UpdateAsync(CompanySettingsDto input);
}
