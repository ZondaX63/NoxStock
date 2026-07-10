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

        myGroup.AddPermission(NexStockPermissions.Dashboard.Default, L("Permission:Dashboard"));

        var productsPermission = AddCrudPermissions(myGroup, NexStockPermissions.Products.Default, L("Permission:Products"),
            NexStockPermissions.Products.Create, NexStockPermissions.Products.Edit, NexStockPermissions.Products.Delete);
        productsPermission.AddChild(NexStockPermissions.Products.Export, L("Permission:Products.Export"));

        AddCrudPermissions(myGroup, NexStockPermissions.Brands.Default, L("Permission:Brands"),
            NexStockPermissions.Brands.Create, NexStockPermissions.Brands.Edit, NexStockPermissions.Brands.Delete);

        AddCrudPermissions(myGroup, NexStockPermissions.Categories.Default, L("Permission:Categories"),
            NexStockPermissions.Categories.Create, NexStockPermissions.Categories.Edit, NexStockPermissions.Categories.Delete);

        var stockMovementsPermission = myGroup.AddPermission(NexStockPermissions.StockMovements.Default, L("Permission:StockMovements"));
        stockMovementsPermission.AddChild(NexStockPermissions.StockMovements.Create, L("Permission:StockMovements.Create"));
        stockMovementsPermission.AddChild(NexStockPermissions.StockMovements.Adjust, L("Permission:StockMovements.Adjust"));

        AddCrudPermissions(myGroup, NexStockPermissions.BusinessPartners.Default, L("Permission:BusinessPartners"),
            NexStockPermissions.BusinessPartners.Create, NexStockPermissions.BusinessPartners.Edit, NexStockPermissions.BusinessPartners.Delete);

        var accountsPermission = AddCrudPermissions(myGroup, NexStockPermissions.Accounts.Default, L("Permission:Accounts"),
            NexStockPermissions.Accounts.Create, NexStockPermissions.Accounts.Edit, NexStockPermissions.Accounts.Delete);
        accountsPermission.AddChild(NexStockPermissions.Accounts.Transfer, L("Permission:Accounts.Transfer"));

        var documentsPermission = AddCrudPermissions(myGroup, NexStockPermissions.Documents.Default, L("Permission:Documents"),
            NexStockPermissions.Documents.Create, NexStockPermissions.Documents.Edit, NexStockPermissions.Documents.Delete);
        documentsPermission.AddChild(NexStockPermissions.Documents.Approve, L("Permission:Documents.Approve"));
        documentsPermission.AddChild(NexStockPermissions.Documents.Cancel, L("Permission:Documents.Cancel"));

        var offersPermission = AddCrudPermissions(myGroup, NexStockPermissions.Offers.Default, L("Permission:Offers"),
            NexStockPermissions.Offers.Create, NexStockPermissions.Offers.Edit, NexStockPermissions.Offers.Delete);
        offersPermission.AddChild(NexStockPermissions.Offers.Convert, L("Permission:Offers.Convert"));

        var ordersPermission = AddCrudPermissions(myGroup, NexStockPermissions.Orders.Default, L("Permission:Orders"),
            NexStockPermissions.Orders.Create, NexStockPermissions.Orders.Edit, NexStockPermissions.Orders.Delete);
        ordersPermission.AddChild(NexStockPermissions.Orders.Convert, L("Permission:Orders.Convert"));

        var salesPermission = myGroup.AddPermission(NexStockPermissions.Sales.Default, L("Permission:Sales"));
        salesPermission.AddChild(NexStockPermissions.Sales.Create, L("Permission:Sales.Create"));
        salesPermission.AddChild(NexStockPermissions.Sales.Cancel, L("Permission:Sales.Cancel"));

        var reportsPermission = myGroup.AddPermission(NexStockPermissions.Reports.Default, L("Permission:Reports"));
        reportsPermission.AddChild(NexStockPermissions.Reports.Export, L("Permission:Reports.Export"));

        var settingsPermission = myGroup.AddPermission(NexStockPermissions.Settings.Default, L("Permission:Settings"));
        settingsPermission.AddChild(NexStockPermissions.Settings.Edit, L("Permission:Settings.Edit"));

        var personnelPermission = myGroup.AddPermission(NexStockPermissions.Personnel.Default, L("Permission:Personnel"));
        personnelPermission.AddChild(NexStockPermissions.Personnel.ManagePermissions, L("Permission:Personnel.ManagePermissions"));

        myGroup.AddPermission(NexStockPermissions.PrintLabels.Default, L("Permission:PrintLabels"));
    }

    private static PermissionDefinition AddCrudPermissions(
        PermissionGroupDefinition group,
        string defaultPermissionName,
        LocalizableString displayName,
        string createPermissionName,
        string editPermissionName,
        string deletePermissionName)
    {
        var permission = group.AddPermission(defaultPermissionName, displayName);
        var localizationPrefix = "Permission:" + defaultPermissionName[(NexStockPermissions.GroupName.Length + 1)..];
        permission.AddChild(createPermissionName, L(localizationPrefix + ".Create"));
        permission.AddChild(editPermissionName, L(localizationPrefix + ".Edit"));
        permission.AddChild(deletePermissionName, L(localizationPrefix + ".Delete"));
        return permission;
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<NexStockResource>(name);
    }
}
