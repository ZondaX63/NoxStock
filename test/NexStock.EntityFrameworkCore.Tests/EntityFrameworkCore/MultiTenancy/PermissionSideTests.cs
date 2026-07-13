using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NexStock.Permissions;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace NexStock.EntityFrameworkCore.MultiTenancy;

[Collection(NexStockTestConsts.CollectionDefinitionName)]
public class PermissionSideTests : NexStockEntityFrameworkCoreTestBase
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
            NexStockPermissions.Products.Default,
            NexStockPermissions.Products.Create,
            NexStockPermissions.Documents.Approve,
            NexStockPermissions.Reports.Default,
            NexStockPermissions.Settings.Edit,
            NexStockPermissions.Personnel.ManagePermissions
        ];

        foreach (var permissionName in permissionNames)
        {
            var permission = await _permissionDefinitionManager.GetAsync(permissionName);
            permission.MultiTenancySide.ShouldBe(MultiTenancySides.Tenant);
        }

        var hostPermission = await _permissionDefinitionManager.GetAsync(NexStockPermissions.ExchangeRates.Refresh);
        hostPermission.MultiTenancySide.ShouldBe(MultiTenancySides.Host);
    }
}
