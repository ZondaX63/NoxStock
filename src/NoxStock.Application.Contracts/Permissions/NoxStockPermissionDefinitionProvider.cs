using NoxStock.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace NoxStock.Permissions;

public class NoxStockPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(NoxStockPermissions.GroupName);

        AddTenantPermission(myGroup, NoxStockPermissions.Dashboard.Default, L("Permission:Dashboard"));

        var productsPermission = AddCrudPermissions(myGroup, NoxStockPermissions.Products.Default, L("Permission:Products"),
            NoxStockPermissions.Products.Create, NoxStockPermissions.Products.Edit, NoxStockPermissions.Products.Delete);
        AddTenantChild(productsPermission, NoxStockPermissions.Products.Export, L("Permission:Products.Export"));

        AddCrudPermissions(myGroup, NoxStockPermissions.Brands.Default, L("Permission:Brands"),
            NoxStockPermissions.Brands.Create, NoxStockPermissions.Brands.Edit, NoxStockPermissions.Brands.Delete);

        AddCrudPermissions(myGroup, NoxStockPermissions.Categories.Default, L("Permission:Categories"),
            NoxStockPermissions.Categories.Create, NoxStockPermissions.Categories.Edit, NoxStockPermissions.Categories.Delete);

        var stockMovementsPermission = AddTenantPermission(myGroup, NoxStockPermissions.StockMovements.Default, L("Permission:StockMovements"));
        AddTenantChild(stockMovementsPermission, NoxStockPermissions.StockMovements.Create, L("Permission:StockMovements.Create"));
        AddTenantChild(stockMovementsPermission, NoxStockPermissions.StockMovements.Adjust, L("Permission:StockMovements.Adjust"));

        var inventoryCountsPermission = AddTenantPermission(myGroup, NoxStockPermissions.InventoryCounts.Default, L("Permission:InventoryCounts"));
        AddTenantChild(inventoryCountsPermission, NoxStockPermissions.InventoryCounts.Create, L("Permission:InventoryCounts.Create"));
        AddTenantChild(inventoryCountsPermission, NoxStockPermissions.InventoryCounts.Complete, L("Permission:InventoryCounts.Complete"));

        var businessPartnersPermission = AddCrudPermissions(myGroup, NoxStockPermissions.BusinessPartners.Default, L("Permission:BusinessPartners"),
            NoxStockPermissions.BusinessPartners.Create, NoxStockPermissions.BusinessPartners.Edit, NoxStockPermissions.BusinessPartners.Delete);
        AddTenantChild(businessPartnersPermission, NoxStockPermissions.BusinessPartners.Transfer, L("Permission:BusinessPartners.Transfer"));

        var accountsPermission = AddCrudPermissions(myGroup, NoxStockPermissions.Accounts.Default, L("Permission:Accounts"),
            NoxStockPermissions.Accounts.Create, NoxStockPermissions.Accounts.Edit, NoxStockPermissions.Accounts.Delete);
        AddTenantChild(accountsPermission, NoxStockPermissions.Accounts.Transfer, L("Permission:Accounts.Transfer"));

        var documentsPermission = AddCrudPermissions(myGroup, NoxStockPermissions.Documents.Default, L("Permission:Documents"),
            NoxStockPermissions.Documents.Create, NoxStockPermissions.Documents.Edit, NoxStockPermissions.Documents.Delete);
        AddTenantChild(documentsPermission, NoxStockPermissions.Documents.Approve, L("Permission:Documents.Approve"));
        AddTenantChild(documentsPermission, NoxStockPermissions.Documents.Cancel, L("Permission:Documents.Cancel"));

        var offersPermission = AddCrudPermissions(myGroup, NoxStockPermissions.Offers.Default, L("Permission:Offers"),
            NoxStockPermissions.Offers.Create, NoxStockPermissions.Offers.Edit, NoxStockPermissions.Offers.Delete);
        AddTenantChild(offersPermission, NoxStockPermissions.Offers.Convert, L("Permission:Offers.Convert"));

        var ordersPermission = AddCrudPermissions(myGroup, NoxStockPermissions.Orders.Default, L("Permission:Orders"),
            NoxStockPermissions.Orders.Create, NoxStockPermissions.Orders.Edit, NoxStockPermissions.Orders.Delete);
        AddTenantChild(ordersPermission, NoxStockPermissions.Orders.Convert, L("Permission:Orders.Convert"));

        var salesPermission = AddTenantPermission(myGroup, NoxStockPermissions.Sales.Default, L("Permission:Sales"));
        AddTenantChild(salesPermission, NoxStockPermissions.Sales.Create, L("Permission:Sales.Create"));
        AddTenantChild(salesPermission, NoxStockPermissions.Sales.Cancel, L("Permission:Sales.Cancel"));

        var reportsPermission = AddTenantPermission(myGroup, NoxStockPermissions.Reports.Default, L("Permission:Reports"));
        AddTenantChild(reportsPermission, NoxStockPermissions.Reports.Export, L("Permission:Reports.Export"));

        AddTenantPermission(myGroup, NoxStockPermissions.Notifications.Default, L("Permission:Notifications"));
        myGroup.AddPermission(
            NoxStockPermissions.AuditLogs.Default,
            L("Permission:AuditLogs"),
            multiTenancySide: MultiTenancySides.Host);

        var settingsPermission = AddTenantPermission(myGroup, NoxStockPermissions.Settings.Default, L("Permission:Settings"));
        AddTenantChild(settingsPermission, NoxStockPermissions.Settings.Edit, L("Permission:Settings.Edit"));

        var personnelPermission = AddTenantPermission(myGroup, NoxStockPermissions.Personnel.Default, L("Permission:Personnel"));
        AddTenantChild(personnelPermission, NoxStockPermissions.Personnel.ManagePermissions, L("Permission:Personnel.ManagePermissions"));

        AddTenantPermission(myGroup, NoxStockPermissions.PrintLabels.Default, L("Permission:PrintLabels"));
        myGroup.AddPermission(
            NoxStockPermissions.ExchangeRates.Refresh,
            L("Permission:ExchangeRates.Refresh"),
            multiTenancySide: MultiTenancySides.Host);

        var licensesPermission = myGroup.AddPermission(
            NoxStockPermissions.Licenses.Default,
            L("Permission:Licenses"),
            multiTenancySide: MultiTenancySides.Host);
        licensesPermission.AddChild(
            NoxStockPermissions.Licenses.Manage,
            L("Permission:Licenses.Manage"),
            multiTenancySide: MultiTenancySides.Host);
        licensesPermission.AddChild(
            NoxStockPermissions.Licenses.ViewAudit,
            L("Permission:Licenses.ViewAudit"),
            multiTenancySide: MultiTenancySides.Host);
    }

    private static PermissionDefinition AddCrudPermissions(
        PermissionGroupDefinition group,
        string defaultPermissionName,
        LocalizableString displayName,
        string createPermissionName,
        string editPermissionName,
        string deletePermissionName)
    {
        var permission = AddTenantPermission(group, defaultPermissionName, displayName);
        var localizationPrefix = "Permission:" + defaultPermissionName[(NoxStockPermissions.GroupName.Length + 1)..];
        AddTenantChild(permission, createPermissionName, L(localizationPrefix + ".Create"));
        AddTenantChild(permission, editPermissionName, L(localizationPrefix + ".Edit"));
        AddTenantChild(permission, deletePermissionName, L(localizationPrefix + ".Delete"));
        return permission;
    }

    private static PermissionDefinition AddTenantPermission(
        PermissionGroupDefinition group,
        string name,
        LocalizableString displayName)
    {
        return group.AddPermission(name, displayName, multiTenancySide: MultiTenancySides.Tenant);
    }

    private static PermissionDefinition AddTenantChild(
        PermissionDefinition parent,
        string name,
        LocalizableString displayName)
    {
        return parent.AddChild(name, displayName, multiTenancySide: MultiTenancySides.Tenant);
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<NoxStockResource>(name);
    }
}
