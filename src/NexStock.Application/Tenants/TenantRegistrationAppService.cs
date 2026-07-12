using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace NexStock.Tenants;

[AllowAnonymous]
public class TenantRegistrationAppService : NexStockAppService, ITenantRegistrationAppService
{
    private readonly ITenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IDataSeeder _dataSeeder;

    public TenantRegistrationAppService(
        ITenantManager tenantManager,
        ITenantRepository tenantRepository,
        IDataSeeder dataSeeder)
    {
        _tenantManager = tenantManager;
        _tenantRepository = tenantRepository;
        _dataSeeder = dataSeeder;
    }

    public async Task RegisterTenantAsync(RegisterTenantDto input)
    {
        var existingTenant = await _tenantRepository.FindByNameAsync(input.TenantName);
        if (existingTenant != null)
        {
            throw new UserFriendlyException("Bu şirket adı daha önce alınmış.");
        }

        var tenant = await _tenantManager.CreateAsync(input.TenantName);
        
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
