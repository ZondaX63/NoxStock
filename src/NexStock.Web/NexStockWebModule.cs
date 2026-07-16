using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NexStock.EntityFrameworkCore;
using NexStock.Localization;
using NexStock.MultiTenancy;
using NexStock.Permissions;
using NexStock.Web.Menus;
using NexStock.Web.HealthChecks;
using Microsoft.OpenApi;
using Volo.Abp;
using Volo.Abp.Studio;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.Localization;
using Volo.Abp.AspNetCore.Mvc.UI;
using Volo.Abp.AspNetCore.Mvc.UI.Bootstrap;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite.Bundling;
using Volo.Abp.Autofac;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.PermissionManagement.Web;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.UI;
using Volo.Abp.UI.Navigation;
using Volo.Abp.VirtualFileSystem;
using Volo.Abp.Identity.Web;
using Volo.Abp.FeatureManagement;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using Volo.Abp.TenantManagement.Web;
using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared.Toolbars;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Identity;
using Volo.Abp.Swashbuckle;
using Volo.Abp.OpenIddict;
using Volo.Abp.Security.Claims;
using Volo.Abp.SettingManagement.Web;
using Volo.Abp.Studio.Client.AspNetCore;
using Volo.Abp.Threading;
using NexStock.Web.ExchangeRates;
using NexStock.Web.Diagnostics;
using NexStock.Web.MultiTenancy;

namespace NexStock.Web;

[DependsOn(
    typeof(NexStockHttpApiModule),
    typeof(NexStockApplicationModule),
    typeof(NexStockEntityFrameworkCoreModule),
    typeof(AbpAutofacModule),
    typeof(AbpStudioClientAspNetCoreModule),
    typeof(AbpIdentityWebModule),
    typeof(AbpAspNetCoreMvcUiLeptonXLiteThemeModule),
    typeof(AbpAccountWebOpenIddictModule),
    typeof(AbpTenantManagementWebModule),
    typeof(AbpFeatureManagementWebModule),
    typeof(AbpSwashbuckleModule),
    typeof(AbpAspNetCoreSerilogModule)
)]
public class NexStockWebModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        context.Services.PreConfigure<AbpMvcDataAnnotationsLocalizationOptions>(options =>
        {
            options.AddAssemblyResource(
                typeof(NexStockResource),
                typeof(NexStockDomainModule).Assembly,
                typeof(NexStockDomainSharedModule).Assembly,
                typeof(NexStockApplicationModule).Assembly,
                typeof(NexStockApplicationContractsModule).Assembly,
                typeof(NexStockWebModule).Assembly
            );
        });

        PreConfigure<OpenIddictBuilder>(builder =>
        {
            builder.AddValidation(options =>
            {
                options.AddAudiences("NexStock");
                options.UseLocalServer();
                options.UseAspNetCore();
            });
        });

        if (!hostingEnvironment.IsDevelopment())
        {
            PreConfigure<AbpOpenIddictAspNetCoreOptions>(options =>
            {
                options.AddDevelopmentEncryptionAndSigningCertificate = false;
            });

            PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
            {
                serverBuilder.AddProductionEncryptionAndSigningCertificate("openiddict.pfx", configuration["AuthServer:CertificatePassPhrase"]!);
                serverBuilder.SetIssuer(new Uri(configuration["AuthServer:Authority"]!));
            });
        }
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        if (!configuration.GetValue<bool>("App:DisablePII"))
        {
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.LogCompleteSecurityArtifact = true;
        }

        if (!configuration.GetValue<bool>("AuthServer:RequireHttpsMetadata"))
        {
            Configure<OpenIddictServerAspNetCoreOptions>(options =>
            {
                options.DisableTransportSecurityRequirement = true;
            });
            
            Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });
        }

        if (hostingEnvironment.IsDevelopment())
        {
            context.Services.AddRazorPages()
                .AddRazorRuntimeCompilation();
        }

        ConfigureStudio(hostingEnvironment);
        context.Services.AddHostedService<ExchangeRateRefreshHostedService>();
        ConfigureBundles(hostingEnvironment);
        ConfigureUrls(configuration);
        ConfigureHealthChecks(context);
        ConfigureAuthentication(context);
        ConfigureVirtualFileSystem(hostingEnvironment);
        ConfigureNavigationServices();
        ConfigureAutoApiControllers();
        ConfigureSwaggerServices(context.Services);

        Configure<PermissionManagementOptions>(options =>
        {
            options.IsDynamicPermissionStoreEnabled = true;
        });

        Configure<Volo.Abp.AspNetCore.Mvc.AntiForgery.AbpAntiForgeryOptions>(options =>
        {
            options.TokenCookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            options.TokenCookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
        });
        
        Configure<Volo.Abp.MultiTenancy.AbpTenantResolveOptions>(options =>
        {
            options.TenantResolvers.Clear();
            options.TenantResolvers.Add(new Volo.Abp.MultiTenancy.CurrentUserTenantResolveContributor());
            options.TenantResolvers.Add(new TenantPathResolveContributor());
        });
        
        Configure<RazorPagesOptions>(options =>
        {
            options.Conventions.AuthorizePage("/Dashboard/Index");
            options.Conventions.AuthorizeFolder("/Products", NexStockPermissions.Products.Default);
            options.Conventions.AuthorizeFolder("/Brands", NexStockPermissions.Brands.Default);
            options.Conventions.AuthorizeFolder("/Categories", NexStockPermissions.Categories.Default);
            options.Conventions.AuthorizePage("/ProductMovements/Index", NexStockPermissions.StockMovements.Default);
            options.Conventions.AuthorizePage("/PrintLabels/Index", NexStockPermissions.PrintLabels.Default);
            options.Conventions.AuthorizeFolder("/Customers", NexStockPermissions.BusinessPartners.Default);
            options.Conventions.AuthorizeFolder("/Suppliers", NexStockPermissions.BusinessPartners.Default);
            options.Conventions.AuthorizePage("/CustomerDetail/Index", NexStockPermissions.BusinessPartners.Default);
            options.Conventions.AuthorizePage("/Accounts/Index", NexStockPermissions.Accounts.Default);
            options.Conventions.AuthorizePage("/Invoices/Index", NexStockPermissions.Documents.Default);
            options.Conventions.AuthorizePage("/Offers/Index", NexStockPermissions.Offers.Default);
            options.Conventions.AuthorizePage("/Orders/Index", NexStockPermissions.Orders.Default);
            options.Conventions.AuthorizePage("/Sales/Index", NexStockPermissions.Sales.Default);
            options.Conventions.AuthorizePage("/Reports/Index", NexStockPermissions.Reports.Default);
            options.Conventions.AuthorizePage("/Notifications/Index", NexStockPermissions.Notifications.Default);
            options.Conventions.AuthorizePage("/AuditLogs/Index", NexStockPermissions.AuditLogs.Default);
            options.Conventions.AuthorizePage("/Settings/Index", NexStockPermissions.Settings.Default);
            options.Conventions.AuthorizePage("/Personnel/Index", NexStockPermissions.Personnel.Default);
        });
    }

    private void ConfigureHealthChecks(ServiceConfigurationContext context)
    {
        context.Services.AddNexStockHealthChecks();
    }

    private void ConfigureStudio(IHostEnvironment hostingEnvironment)
    {
        if (hostingEnvironment.IsProduction())
        {
            Configure<AbpStudioClientOptions>(options =>
            {
                options.IsLinkEnabled = false;
            });
        }
    }

    private void ConfigureBundles(IHostEnvironment hostingEnvironment)
    {
        Configure<AbpBundlingOptions>(options =>
        {
            options.StyleBundles.Configure(
                LeptonXLiteThemeBundles.Styles.Global,
                bundle =>
                {
                    bundle.AddFiles("/global-styles.css");
                    bundle.AddFiles("/tailwind.css");
                }
            );

            options.ScriptBundles.Configure(
                LeptonXLiteThemeBundles.Scripts.Global,
                bundle =>
                {
                    bundle.AddFiles("/global-scripts.js");
                    bundle.AddFiles("/currency-ticker.js");
                    if (hostingEnvironment.IsDevelopment())
                    {
                        bundle.AddFiles("/dev-login-helper.js");
                    }
                }
            );
        });
    }

    private void ConfigureUrls(IConfiguration configuration)
    {
        Configure<AppUrlOptions>(options =>
        {
            options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
        });
    }

    private void ConfigureAuthentication(ServiceConfigurationContext context)
    {
        context.Services.ForwardIdentityAuthenticationForBearer(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        context.Services.PostConfigure<CookieAuthenticationOptions>(
            IdentityConstants.ApplicationScheme,
            options => options.Cookie.Path = "/");
        context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
        {
            options.IsDynamicClaimsEnabled = true;
        });
    }

    private void ConfigureVirtualFileSystem(IWebHostEnvironment hostingEnvironment)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<NexStockWebModule>();

            if (hostingEnvironment.IsDevelopment())
            {
                var domainSharedPath = Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}NexStock.Domain.Shared", Path.DirectorySeparatorChar));
                if (Directory.Exists(domainSharedPath))
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<NexStockDomainSharedModule>(domainSharedPath);
                }

                var domainPath = Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}NexStock.Domain", Path.DirectorySeparatorChar));
                if (Directory.Exists(domainPath))
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<NexStockDomainModule>(domainPath);
                }

                var appContractsPath = Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}NexStock.Application.Contracts", Path.DirectorySeparatorChar));
                if (Directory.Exists(appContractsPath))
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<NexStockApplicationContractsModule>(appContractsPath);
                }

                var appPath = Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}NexStock.Application", Path.DirectorySeparatorChar));
                if (Directory.Exists(appPath))
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<NexStockApplicationModule>(appPath);
                }

                var httpApiPath = Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}src{0}NexStock.HttpApi", Path.DirectorySeparatorChar));
                if (Directory.Exists(httpApiPath))
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<NexStockHttpApiModule>(httpApiPath);
                }

                options.FileSets.ReplaceEmbeddedByPhysical<NexStockWebModule>(hostingEnvironment.ContentRootPath);
            }
        });
    }

    private void ConfigureNavigationServices()
    {
        Configure<AbpNavigationOptions>(options =>
        {
            options.MenuContributors.Add(new NexStockMenuContributor());
        });

        Configure<AbpToolbarOptions>(options =>
        {
            options.Contributors.Add(new NexStockToolbarContributor());
        });
    }

    private void ConfigureAutoApiControllers()
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(NexStockApplicationModule).Assembly);
        });
    }

    private void ConfigureSwaggerServices(IServiceCollection services)
    {
        services.AddAbpSwaggerGen(
            options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "NexStock API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
            }
        );
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        EnsureDefaultUserRolePermissions(context);

        app.UseForwardedHeaders();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseAbpRequestLocalization();

        if (!env.IsDevelopment())
        {
            app.UseErrorPage();
            app.UseHsts();
        }

        app.UseCorrelationId();
        app.UseMiddleware<TenantPathMiddleware>();
        app.UseMiddleware<NexStockExceptionHandlingMiddleware>();
        app.UseRouting();
        app.MapAbpStaticAssets();
        app.UseAbpStudioLink();
        app.UseAbpSecurityHeaders();
        app.UseAuthentication();
        app.UseAbpOpenIddictValidation();

        if (MultiTenancyConsts.IsEnabled)
        {
            app.UseMultiTenancy();
        }

        app.UseUnitOfWork();
        app.UseMiddleware<TenantActivationMiddleware>();
        app.UseMiddleware<PlatformAdministrationMiddleware>();
        app.UseDynamicClaims();
        app.UseAuthorization();

        app.UseSwagger();
        app.UseAbpSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "NexStock API");
        });
        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints();
    }

    private static void EnsureDefaultUserRolePermissions(ApplicationInitializationContext context)
    {
        using var scope = context.ServiceProvider.CreateScope();
        var permissionDataSeeder = scope.ServiceProvider.GetRequiredService<Volo.Abp.PermissionManagement.IPermissionDataSeeder>();
        AsyncHelper.RunSync(() => permissionDataSeeder.SeedAsync(
            Volo.Abp.Authorization.Permissions.RolePermissionValueProvider.ProviderName,
            "user",
            NexStockDefaultUserPermissions.Basic
        ));
        AsyncHelper.RunSync(() => permissionDataSeeder.SeedAsync(
            Volo.Abp.Authorization.Permissions.RolePermissionValueProvider.ProviderName,
            "admin",
            [NexStockPermissions.AuditLogs.Default]
        ));
    }
}
