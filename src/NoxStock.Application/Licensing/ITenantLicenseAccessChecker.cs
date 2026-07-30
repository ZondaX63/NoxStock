using System;
using System.Threading.Tasks;

namespace NoxStock.Licensing;

public interface ITenantLicenseAccessChecker
{
    Task<TenantLicenseAccessDto> GetAsync(Guid tenantId);
    Task<TenantLicenseAccessDto> GetCurrentAsync();
    Task EnsureCanWriteAsync();
    Task EnsureCanWriteAsync(Guid tenantId);
    Task EnsureFeatureAsync(string featureName);
    Task EnsureLimitAsync(string limitName, long currentCount);
    Task RestrictAsync(Guid tenantId, LicenseStatus status);
    Task InvalidateAsync(Guid tenantId);
}
