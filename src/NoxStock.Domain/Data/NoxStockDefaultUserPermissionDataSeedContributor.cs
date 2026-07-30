using System.Threading.Tasks;
using NoxStock.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.PermissionManagement;

namespace NoxStock.Data;

public class NoxStockDefaultUserPermissionDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IPermissionDataSeeder _permissionDataSeeder;

    public NoxStockDefaultUserPermissionDataSeedContributor(IPermissionDataSeeder permissionDataSeeder)
    {
        _permissionDataSeeder = permissionDataSeeder;
    }

    public virtual async Task SeedAsync(DataSeedContext context)
    {
        if (context.TenantId is null)
        {
            await _permissionDataSeeder.SeedAsync(
                RolePermissionValueProvider.ProviderName,
                "admin",
                [
                    "NoxStock.AuditLogs",
                    "NoxStock.Licenses",
                    "NoxStock.Licenses.Manage",
                    "NoxStock.Licenses.ViewAudit"
                ]);
            return;
        }

        await _permissionDataSeeder.SeedAsync(
            RolePermissionValueProvider.ProviderName,
            "user",
            NoxStockDefaultUserPermissions.Basic,
            context.TenantId
        );
    }
}
