using NexStock.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace NexStock.Permissions;

public class NexStockPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(NexStockPermissions.GroupName);

        AddTenantPermission(myGroup, NexStockPermissions.Dashboard.Default, L("Permission:Dashboard"));

        var productsPermission = AddCrudPermissions(myGroup, NexStockPermissions.Products.Default, L("Permission:Products"),
            NexStockPermissions.Products.Create, NexStockPermissions.Products.Edit, NexStockPermissions.Products.Delete);
        AddTenantChild(productsPermission, NexStockPermissions.Products.Export, L("Permission:Products.Export"));

        AddCrudPermissions(myGroup, NexStockPermissions.Brands.Default, L("Permission:Brands"),
            NexStockPermissions.Brands.Create, NexStockPermissions.Brands.Edit, NexStockPermissions.Brands.Delete);

        AddCrudPermissions(myGroup, NexStockPermissions.Categories.Default, L("Permission:Categories"),
            NexStockPermissions.Categories.Create, NexStockPermissions.Categories.Edit, NexStockPermissions.Categories.Delete);

        var stockMovementsPermission = AddTenantPermission(myGroup, NexStockPermissions.StockMovements.Default, L("Permission:StockMovements"));
        AddTenantChild(stockMovementsPermission, NexStockPermissions.StockMovements.Create, L("Permission:StockMovements.Create"));
        AddTenantChild(stockMovementsPermission, NexStockPermissions.StockMovements.Adjust, L("Permission:StockMovements.Adjust"));

        AddCrudPermissions(myGroup, NexStockPermissions.BusinessPartners.Default, L("Permission:BusinessPartners"),
            NexStockPermissions.BusinessPartners.Create, NexStockPermissions.BusinessPartners.Edit, NexStockPermissions.BusinessPartners.Delete);

        var accountsPermission = AddCrudPermissions(myGroup, NexStockPermissions.Accounts.Default, L("Permission:Accounts"),
            NexStockPermissions.Accounts.Create, NexStockPermissions.Accounts.Edit, NexStockPermissions.Accounts.Delete);
        AddTenantChild(accountsPermission, NexStockPermissions.Accounts.Transfer, L("Permission:Accounts.Transfer"));

        var documentsPermission = AddCrudPermissions(myGroup, NexStockPermissions.Documents.Default, L("Permission:Documents"),
            NexStockPermissions.Documents.Create, NexStockPermissions.Documents.Edit, NexStockPermissions.Documents.Delete);
        AddTenantChild(documentsPermission, NexStockPermissions.Documents.Approve, L("Permission:Documents.Approve"));
        AddTenantChild(documentsPermission, NexStockPermissions.Documents.Cancel, L("Permission:Documents.Cancel"));

        var offersPermission = AddCrudPermissions(myGroup, NexStockPermissions.Offers.Default, L("Permission:Offers"),
            NexStockPermissions.Offers.Create, NexStockPermissions.Offers.Edit, NexStockPermissions.Offers.Delete);
        AddTenantChild(offersPermission, NexStockPermissions.Offers.Convert, L("Permission:Offers.Convert"));

        var ordersPermission = AddCrudPermissions(myGroup, NexStockPermissions.Orders.Default, L("Permission:Orders"),
            NexStockPermissions.Orders.Create, NexStockPermissions.Orders.Edit, NexStockPermissions.Orders.Delete);
        AddTenantChild(ordersPermission, NexStockPermissions.Orders.Convert, L("Permission:Orders.Convert"));

        var salesPermission = AddTenantPermission(myGroup, NexStockPermissions.Sales.Default, L("Permission:Sales"));
        AddTenantChild(salesPermission, NexStockPermissions.Sales.Create, L("Permission:Sales.Create"));
        AddTenantChild(salesPermission, NexStockPermissions.Sales.Cancel, L("Permission:Sales.Cancel"));

        var reportsPermission = AddTenantPermission(myGroup, NexStockPermissions.Reports.Default, L("Permission:Reports"));
        AddTenantChild(reportsPermission, NexStockPermissions.Reports.Export, L("Permission:Reports.Export"));

        AddTenantPermission(myGroup, NexStockPermissions.Notifications.Default, L("Permission:Notifications"));
        AddTenantPermission(myGroup, NexStockPermissions.AuditLogs.Default, L("Permission:AuditLogs"));

        var settingsPermission = AddTenantPermission(myGroup, NexStockPermissions.Settings.Default, L("Permission:Settings"));
        AddTenantChild(settingsPermission, NexStockPermissions.Settings.Edit, L("Permission:Settings.Edit"));

        var personnelPermission = AddTenantPermission(myGroup, NexStockPermissions.Personnel.Default, L("Permission:Personnel"));
        AddTenantChild(personnelPermission, NexStockPermissions.Personnel.ManagePermissions, L("Permission:Personnel.ManagePermissions"));

        AddTenantPermission(myGroup, NexStockPermissions.PrintLabels.Default, L("Permission:PrintLabels"));
        myGroup.AddPermission(
            NexStockPermissions.ExchangeRates.Refresh,
            L("Permission:ExchangeRates.Refresh"),
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
        var localizationPrefix = "Permission:" + defaultPermissionName[(NexStockPermissions.GroupName.Length + 1)..];
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
        return LocalizableString.Create<NexStockResource>(name);
    }
}
