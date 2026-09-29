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
        AddTenantChild(businessPartnersPermission, NoxStockPermissions.BusinessPartners.OpeningBalance, L("Permission:BusinessPartners.OpeningBalance"));
        AddTenantChild(businessPartnersPermission, NoxStockPermissions.BusinessPartners.CancelOpeningBalance, L("Permission:BusinessPartners.CancelOpeningBalance"));
        AddTenantChild(businessPartnersPermission, NoxStockPermissions.BusinessPartners.Collection, L("Permission:BusinessPartners.Collection"));
        AddTenantChild(businessPartnersPermission, NoxStockPermissions.BusinessPartners.CancelCollection, L("Permission:BusinessPartners.CancelCollection"));
        AddTenantChild(businessPartnersPermission, NoxStockPermissions.BusinessPartners.Payment, L("Permission:BusinessPartners.Payment"));
        AddTenantChild(businessPartnersPermission, NoxStockPermissions.BusinessPartners.CancelPayment, L("Permission:BusinessPartners.CancelPayment"));

        var accountsPermission = AddCrudPermissions(myGroup, NoxStockPermissions.Accounts.Default, L("Permission:Accounts"),
            NoxStockPermissions.Accounts.Create, NoxStockPermissions.Accounts.Edit, NoxStockPermissions.Accounts.Delete);
        AddTenantChild(accountsPermission, NoxStockPermissions.Accounts.Transfer, L("Permission:Accounts.Transfer"));
        AddTenantChild(accountsPermission, NoxStockPermissions.Accounts.CancelTransfer, L("Permission:Accounts.CancelTransfer"));
        AddTenantChild(accountsPermission, NoxStockPermissions.Accounts.OpeningBalance, L("Permission:Accounts.OpeningBalance"));
        AddTenantChild(accountsPermission, NoxStockPermissions.Accounts.CancelOpeningBalance, L("Permission:Accounts.CancelOpeningBalance"));
        AddTenantChild(accountsPermission, NoxStockPermissions.Accounts.AddTransaction, L("Permission:Accounts.AddTransaction"));
        AddTenantChild(accountsPermission, NoxStockPermissions.Accounts.CancelTransaction, L("Permission:Accounts.CancelTransaction"));

        var documentsPermission = AddCrudPermissions(myGroup, NoxStockPermissions.Documents.Default, L("Permission:Documents"),
            NoxStockPermissions.Documents.Create, NoxStockPermissions.Documents.Edit, NoxStockPermissions.Documents.Delete);
        AddTenantChild(documentsPermission, NoxStockPermissions.Documents.Approve, L("Permission:Documents.Approve"));
        AddTenantChild(documentsPermission, NoxStockPermissions.Documents.Cancel, L("Permission:Documents.Cancel"));
        AddTenantChild(documentsPermission, NoxStockPermissions.Documents.SellBelowCost, L("Permission:Documents.SellBelowCost"));

        var offersPermission = AddCrudPermissions(myGroup, NoxStockPermissions.Offers.Default, L("Permission:Offers"),
            NoxStockPermissions.Offers.Create, NoxStockPermissions.Offers.Edit, NoxStockPermissions.Offers.Delete);
        AddTenantChild(offersPermission, NoxStockPermissions.Offers.Convert, L("Permission:Offers.Convert"));

        var ordersPermission = AddCrudPermissions(myGroup, NoxStockPermissions.Orders.Default, L("Permission:Orders"),
            NoxStockPermissions.Orders.Create, NoxStockPermissions.Orders.Edit, NoxStockPermissions.Orders.Delete);
        AddTenantChild(ordersPermission, NoxStockPermissions.Orders.Convert, L("Permission:Orders.Convert"));

        var salesPermission = AddTenantPermission(myGroup, NoxStockPermissions.Sales.Default, L("Permission:Sales"));
        AddTenantChild(salesPermission, NoxStockPermissions.Sales.Create, L("Permission:Sales.Create"));
        AddTenantChild(salesPermission, NoxStockPermissions.Sales.Cancel, L("Permission:Sales.Cancel"));
        AddTenantChild(salesPermission, NoxStockPermissions.Sales.SellBelowCost, L("Permission:Sales.SellBelowCost"));

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
        var backupsPermission = myGroup.AddPermission(
            NoxStockPermissions.TenantBackups.Default,
            L("Permission:TenantBackups"),
            multiTenancySide: MultiTenancySides.Both);
        backupsPermission.AddChild(NoxStockPermissions.TenantBackups.Create, L("Permission:TenantBackups.Create"), multiTenancySide: MultiTenancySides.Tenant);
        backupsPermission.AddChild(NoxStockPermissions.TenantBackups.Download, L("Permission:TenantBackups.Download"), multiTenancySide: MultiTenancySides.Both);
        backupsPermission.AddChild(NoxStockPermissions.TenantBackups.Delete, L("Permission:TenantBackups.Delete"), multiTenancySide: MultiTenancySides.Both);
        backupsPermission.AddChild(NoxStockPermissions.TenantBackups.Restore, L("Permission:TenantBackups.Restore"), multiTenancySide: MultiTenancySides.Host);
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
