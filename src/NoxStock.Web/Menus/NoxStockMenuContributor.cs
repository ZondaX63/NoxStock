using System.Threading.Tasks;
using NoxStock.Localization;
using NoxStock.Permissions;
using Volo.Abp.SettingManagement.Web.Navigation;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity.Web.Navigation;
using Volo.Abp.UI.Navigation;
using Volo.Abp.TenantManagement.Web.Navigation;
using Volo.Abp.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
namespace NoxStock.Web.Menus;
public class NoxStockMenuContributor : IMenuContributor
{
    public async Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name == StandardMenus.Main)
        {
            await ConfigureMainMenuAsync(context);
        }
    }
    private static Task ConfigureMainMenuAsync(MenuConfigurationContext context)
    {
        var l = context.GetLocalizer<NoxStockResource>();
        //Home
        context.Menu.AddItem(
            new ApplicationMenuItem(
                NoxStockMenus.Home,
                l["Menu:Home"],
                "~/",
                icon: "fa fa-home",
                order: 1
            )
        );
        //Administration
        var administration = context.Menu.GetAdministration();
        administration.Order = 6;
        //Administration->Identity
        administration.SetSubItemOrder(IdentityMenuNames.GroupName, 1);

        administration.SetSubItemOrder(TenantManagementMenuNames.GroupName, 1);

        administration.SetSubItemOrder(SettingManagementMenuNames.GroupName, 3);
        //Administration->Settings
        administration.SetSubItemOrder(SettingManagementMenuNames.GroupName, 8);
        administration.SetSubItemOrder(SettingManagementMenuNames.GroupName, 8);

        var currentTenant = context.ServiceProvider.GetRequiredService<ICurrentTenant>();
        if (currentTenant.IsAvailable)
        {
            context.Menu.AddItem(
            new ApplicationMenuItem(
                "Inventory",
                l["Menu:Inventory"],
                icon: "fa fa-box"
            ).AddItem(
                new ApplicationMenuItem(
                    "Inventory.Products",
                    l["Menu:Products"],
                    url: "/Products"
                ).RequirePermissions(NoxStockPermissions.Products.Default)
            ).AddItem(
                new ApplicationMenuItem(
                    "Inventory.StockMovements",
                    l["Menu:StockMovements"],
                    url: "/ProductMovements"
                ).RequirePermissions(NoxStockPermissions.StockMovements.Default)
            ).AddItem(
                new ApplicationMenuItem(
                    "Inventory.InventoryCounts",
                    l["Menu:InventoryCounts"],
                    url: "/InventoryCounts"
                ).RequirePermissions(NoxStockPermissions.InventoryCounts.Default)
            ).AddItem(
                new ApplicationMenuItem(
                    "Inventory.PrintLabels",
                    l["Menu:PrintLabels"],
                    url: "/PrintLabels"
                ).RequirePermissions(NoxStockPermissions.PrintLabels.Default)
            )
        );

        context.Menu.AddItem(
            new ApplicationMenuItem(
                "Commercial",
                l["Menu:Commercial"],
                icon: "fa fa-receipt"
            ).AddItem(
                new ApplicationMenuItem(
                    "Commercial.Sales",
                    l["Menu:Sales"],
                    url: "/Sales"
                ).RequirePermissions(NoxStockPermissions.Sales.Default)
            ).AddItem(
                new ApplicationMenuItem(
                    "Commercial.Invoices",
                    l["Menu:Invoices"],
                    url: "/Invoices"
                ).RequirePermissions(NoxStockPermissions.Documents.Default)
            ).AddItem(
                new ApplicationMenuItem(
                    "Commercial.Offers",
                    l["Menu:Offers"],
                    url: "/Offers"
                ).RequirePermissions(NoxStockPermissions.Offers.Default)
            ).AddItem(
                new ApplicationMenuItem(
                    "Commercial.Orders",
                    l["Menu:Orders"],
                    url: "/Orders"
                ).RequirePermissions(NoxStockPermissions.Orders.Default)
            )
        );

        context.Menu.AddItem(
            new ApplicationMenuItem(
                "Finance",
                l["Menu:Finance"],
                icon: "fa fa-wallet"
            ).AddItem(
                new ApplicationMenuItem(
                    "Finance.Accounts",
                    l["Menu:Accounts"],
                    url: "/Accounts"
                ).RequirePermissions(NoxStockPermissions.Accounts.Default)
            ).AddItem(
                new ApplicationMenuItem(
                    "Finance.BusinessPartners",
                    "Cari Hesaplar",
                    url: "/BusinessPartners"
                ).RequirePermissions(NoxStockPermissions.BusinessPartners.Default)
            )
        );

        context.Menu.AddItem(
            new ApplicationMenuItem(
                "Management",
                l["Menu:Management"],
                icon: "fa fa-chart-line"
            ).AddItem(
                new ApplicationMenuItem(
                    "Management.Reports",
                    l["Menu:Reports"],
                    url: "/Reports"
                ).RequirePermissions(NoxStockPermissions.Reports.Default)
            ).AddItem(
                new ApplicationMenuItem(
                    "Management.Personnel",
                    l["Menu:Personnel"],
                    url: "/Personnel"
                ).RequirePermissions(NoxStockPermissions.Personnel.Default)
            ).AddItem(
                new ApplicationMenuItem(
                    "Management.Settings",
                    l["Menu:Settings"],
                    url: "/Settings"
                ).RequirePermissions(NoxStockPermissions.Settings.Default)
            )
        );
        }

        return Task.CompletedTask;
    }
}
