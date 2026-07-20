using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using NoxStock.MultiTenancy;
using Volo.Abp.Auditing;

namespace NoxStock.Tenants;

[AllowAnonymous]
[DisableAuditing]
public class TenantRegistrationAppService : NoxStockAppService, ITenantRegistrationAppService
{
    private readonly ITenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IDataSeeder _dataSeeder;
    private readonly IConfiguration _configuration;

    public TenantRegistrationAppService(
        ITenantManager tenantManager,
        ITenantRepository tenantRepository,
        IDataSeeder dataSeeder,
        IConfiguration configuration)
    {
        _tenantManager = tenantManager;
        _tenantRepository = tenantRepository;
        _dataSeeder = dataSeeder;
        _configuration = configuration;
    }

    public async Task RegisterTenantAsync(RegisterTenantDto input)
    {
        if (!_configuration.GetValue<bool>("App:PublicRegistrationEnabled"))
        {
            throw new BusinessException("NoxStock:PublicRegistrationDisabled");
        }

        var existingTenant = await _tenantRepository.FindByNameAsync(input.TenantName.ToUpperInvariant());
        if (existingTenant != null)
        {
            throw new UserFriendlyException("Bu şirket adı daha önce alınmış.");
        }

        var tenant = await _tenantManager.CreateAsync(input.TenantName);
        tenant.SetNoxStockActive(true);
        
        await _tenantRepository.InsertAsync(tenant, autoSave: true);

        using (CurrentTenant.Change(tenant.Id))
        {
            await _dataSeeder.SeedAsync(
                new DataSeedContext(tenant.Id)
                    .WithProperty("AdminEmail", input.AdminEmailAddress)
                    .WithProperty("AdminPassword", input.AdminPassword)
            );
        }
    }
}
