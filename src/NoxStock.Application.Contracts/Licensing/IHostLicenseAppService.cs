using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace NoxStock.Licensing;

public interface IHostLicenseAppService : IApplicationService
{
    Task<List<HostTenantLicenseRowDto>> GetOverviewAsync();
    Task<List<LicensePlanDto>> GetPlansAsync();
    Task<TenantLicenseDto> GetAsync(Guid id);
    Task<List<TenantLicenseAuditLogDto>> GetAuditLogAsync(Guid tenantLicenseId);
    Task<TenantLicenseDto> AssignAsync(AssignTenantLicenseDto input);
    Task<TenantLicenseDto> ChangePlanAsync(Guid id, ChangeTenantLicensePlanDto input);
    Task<TenantLicenseDto> UpdateEntitlementsAsync(Guid id, UpdateTenantLicenseEntitlementsDto input);
    Task<TenantLicenseDto> ExtendAsync(Guid id, ExtendTenantLicenseDto input);
    Task<TenantLicenseDto> SuspendAsync(Guid id, SuspendTenantLicenseDto input);
    Task<TenantLicenseDto> ReactivateAsync(Guid id);
    Task<TenantLicenseDto> ChangeStatusAsync(Guid id, ChangeTenantLicenseStatusDto input);
    Task DeleteAsync(Guid id);
}
