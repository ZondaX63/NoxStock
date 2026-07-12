using System.Threading.Tasks;
using NexStock.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.PermissionManagement;

namespace NexStock.Data;

public class NexStockDefaultUserPermissionDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IPermissionDataSeeder _permissionDataSeeder;

    public NexStockDefaultUserPermissionDataSeedContributor(IPermissionDataSeeder permissionDataSeeder)
    {
        _permissionDataSeeder = permissionDataSeeder;
    }

    public virtual async Task SeedAsync(DataSeedContext context)
    {
        await _permissionDataSeeder.SeedAsync(
            RolePermissionValueProvider.ProviderName,
            "user",
            NexStockDefaultUserPermissions.Basic,
            context.TenantId
        );
    }
}
