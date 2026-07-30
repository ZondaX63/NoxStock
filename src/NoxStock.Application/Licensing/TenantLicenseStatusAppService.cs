using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.MultiTenancy;

namespace NoxStock.Licensing;

[Authorize]
public class TenantLicenseStatusAppService : NoxStockAppService, ITenantLicenseStatusAppService
{
    private readonly ITenantLicenseAccessChecker _accessChecker;

    public TenantLicenseStatusAppService(ITenantLicenseAccessChecker accessChecker)
    {
        _accessChecker = accessChecker;
    }

    public Task<TenantLicenseAccessDto> GetCurrentAsync()
    {
        CurrentTenant.RequireTenantId();
        return _accessChecker.GetCurrentAsync();
    }
}
