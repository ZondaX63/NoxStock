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
using Microsoft.AspNetCore.DataProtection;
using System.IO;
using NoxStock.TenantBackups;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.BackgroundJobs;

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
    ,typeof(AbpBackgroundWorkersModule)
    )]
public class NoxStockApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var dataProtection = context.Services.AddDataProtection().SetApplicationName("NoxStock");
        var keyRingPath = context.Services.GetConfiguration()["DataProtection:KeyRingPath"];
        if (!string.IsNullOrWhiteSpace(keyRingPath))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
        }
        context.Services.AddTransient<ITenantBackupStorage, TenantBackupBlobStorage>();
        context.Services.AddTransient<IBackupEncryptionService, AesGcmBackupEncryptionService>();
        context.Services.AddTransient<ITenantBackupContributor, TenantSettingsBackupContributor>();
        context.Services.AddTransient<ITenantBackupContributor, CatalogBackupContributor>();
        context.Services.AddTransient<ITenantBackupContributor, PartiesBackupContributor>();
        context.Services.AddTransient<ITenantBackupContributor, DocumentsBackupContributor>();
        context.Services.AddTransient<ITenantBackupContributor, InventoryBackupContributor>();
        context.Services.AddTransient<ITenantBackupContributor, FinanceBackupContributor>();
        context.Services.AddTransient<ITenantBackupContributor, CommandsBackupContributor>();
        context.Services.AddTransient<TenantBackupJob>();
        context.Services.AddTransient<TenantCloneJob>();
        Configure<AbpBackgroundJobOptions>(options =>
        {
            options.AddJob<TenantBackupJob>();
            options.AddJob<TenantCloneJob>();
        });
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
