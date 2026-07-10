using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.Account;
using Volo.Abp.Identity;
using Volo.Abp.Mapperly;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;
using System;
using Volo.Abp.TenantManagement;

namespace NexStock;

[DependsOn(
    typeof(NexStockDomainModule),
    typeof(NexStockApplicationContractsModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpAccountApplicationModule),
    typeof(AbpTenantManagementApplicationModule),
    typeof(AbpSettingManagementApplicationModule)
    )]
public class NexStockApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClient("ExchangeRateApi", client =>
        {
            client.BaseAddress = new Uri("https://api.exchangerate-api.com/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
    }
}
