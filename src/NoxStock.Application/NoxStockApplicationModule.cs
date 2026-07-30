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
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using NoxStock.Licensing;

namespace NoxStock;

[DependsOn(
    typeof(NoxStockDomainModule),
    typeof(NoxStockApplicationContractsModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpAccountApplicationModule),
    typeof(AbpTenantManagementApplicationModule),
    typeof(AbpSettingManagementApplicationModule)
    )]
public class NoxStockApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.OnRegistered(registrationContext =>
        {
            if (typeof(IApplicationService).IsAssignableFrom(registrationContext.ImplementationType))
            {
                registrationContext.Interceptors.TryAdd<TenantLicenseWriteInterceptor>();
            }
        });

        context.Services.AddHttpClient("TcmbExchangeRates", client =>
        {
            client.BaseAddress = new Uri("https://www.tcmb.gov.tr/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NoxStock/1.0");
        });
    }
}
