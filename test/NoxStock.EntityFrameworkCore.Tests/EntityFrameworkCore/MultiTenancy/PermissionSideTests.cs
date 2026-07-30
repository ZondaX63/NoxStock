using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.Permissions;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace NoxStock.EntityFrameworkCore.MultiTenancy;

[Collection(NoxStockTestConsts.CollectionDefinitionName)]
public class PermissionSideTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly IPermissionDefinitionManager _permissionDefinitionManager;

    public PermissionSideTests()
    {
        _permissionDefinitionManager = ServiceProvider.GetRequiredService<IPermissionDefinitionManager>();
    }

    [Fact]
    public async Task Commercial_Permissions_Should_Be_Tenant_Only()
    {
        string[] permissionNames =
        [
            NoxStockPermissions.Products.Default,
            NoxStockPermissions.Products.Create,
            NoxStockPermissions.Documents.Approve,
            NoxStockPermissions.Reports.Default,
            NoxStockPermissions.Settings.Edit,
            NoxStockPermissions.Personnel.ManagePermissions
        ];

        foreach (var permissionName in permissionNames)
        {
            var permission = await _permissionDefinitionManager.GetAsync(permissionName);
            permission.MultiTenancySide.ShouldBe(MultiTenancySides.Tenant);
        }

        var hostPermission = await _permissionDefinitionManager.GetAsync(NoxStockPermissions.ExchangeRates.Refresh);
        hostPermission.MultiTenancySide.ShouldBe(MultiTenancySides.Host);

        var licensePermission = await _permissionDefinitionManager.GetAsync(NoxStockPermissions.Licenses.Manage);
        licensePermission.MultiTenancySide.ShouldBe(MultiTenancySides.Host);
    }
}
