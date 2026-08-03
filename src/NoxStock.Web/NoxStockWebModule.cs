using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NoxStock.EntityFrameworkCore;
using NoxStock.Localization;
using NoxStock.MultiTenancy;
using NoxStock.Permissions;
using NoxStock.Web.Menus;
using NoxStock.Web.HealthChecks;
using Volo.Abp;
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
using System.Net;
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
using Volo.Abp.OpenIddict;
using Volo.Abp.Security.Claims;
using Volo.Abp.SettingManagement.Web;
using NoxStock.Web.ExchangeRates;
using NoxStock.Web.Diagnostics;
using NoxStock.Web.MultiTenancy;
using Volo.Abp.Auditing;
using NoxStock.Web.Licensing;

namespace NoxStock.Web;

[DependsOn(
    typeof(NoxStockHttpApiModule),
    typeof(NoxStockApplicationModule),
    typeof(NoxStockEntityFrameworkCoreModule),
    typeof(AbpAutofacModule),
    typeof(AbpIdentityWebModule),
    typeof(AbpAspNetCoreMvcUiLeptonXLiteThemeModule),
    typeof(AbpAccountWebOpenIddictModule),
    typeof(AbpTenantManagementWebModule),
    typeof(AbpFeatureManagementWebModule),
    typeof(AbpAspNetCoreSerilogModule)
)]
public class NoxStockWebModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();
        ProductionSecurityConfigurationValidator.Validate(configuration, hostingEnvironment);

        context.Services.PreConfigure<AbpMvcDataAnnotationsLocalizationOptions>(options =>
        {
            options.AddAssemblyResource(
                typeof(NoxStockResource),
                typeof(NoxStockDomainModule).Assembly,
                typeof(NoxStockDomainSharedModule).Assembly,
                typeof(NoxStockApplicationModule).Assembly,
                typeof(NoxStockApplicationContractsModule).Assembly,
                typeof(NoxStockWebModule).Assembly
            );
        });

        PreConfigure<OpenIddictBuilder>(builder =>
        {
            builder.AddValidation(options =>
            {
                options.AddAudiences("NoxStock");
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
        context.Services.AddHostedService<LicenseLifecycleBackgroundService>();
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        var enablePii = hostingEnvironment.IsDevelopment() &&
                        !configuration.GetValue("App:DisablePII", true);
        Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = enablePii;
        Microsoft.IdentityModel.Logging.IdentityModelEventSource.LogCompleteSecurityArtifact = false;

        Configure<AbpAuditingOptions>(options =>
        {
            options.DisableLogActionInfo = true;
            options.IsEnabledForAnonymousUsers = false;
            options.Contributors.Add(new SensitiveDataAuditLogContributor());
        });

        if (configuration.GetValue<bool>("Proxy:Enabled"))
        {
            Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = configuration.GetValue("Proxy:ForwardLimit", 1);

                foreach (var value in (configuration["Proxy:KnownProxies"] ?? string.Empty)
                             .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!IPAddress.TryParse(value, out var address))
                    {
                        throw new InvalidOperationException($"Proxy:KnownProxies contains an invalid IP address: {value}");
                    }

                    options.KnownProxies.Add(address);
                }
            });
        }
        else if (hostingEnvironment.IsDevelopment() && !configuration.GetValue<bool>("AuthServer:RequireHttpsMetadata"))
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

        // Optional DTO fields (barcode, description, shelf, quantity, etc.)
        // must not become implicitly required just because they are declared
        // as non-nullable CLR properties. Explicit [Required] attributes still
        // remain enforced (for example Product.Name).
        Configure<MvcOptions>(options =>
        {
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        });

        context.Services.AddHostedService<ExchangeRateRefreshHostedService>();
        ConfigureBundles(hostingEnvironment);
        ConfigureUrls(configuration);
        ConfigureHealthChecks(context);
        ConfigureAuthentication(context);
        ConfigureVirtualFileSystem(hostingEnvironment);
        ConfigureNavigationServices();
        ConfigureAutoApiControllers();

        Configure<PermissionManagementOptions>(options =>
        {
            options.IsDynamicPermissionStoreEnabled = true;
        });

        if (hostingEnvironment.IsProduction())
        {
            Configure<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>(options =>
            {
                options.HttpsPort = 443;
            });
        }

        Configure<Volo.Abp.AspNetCore.Mvc.AntiForgery.AbpAntiForgeryOptions>(options =>
        {
            options.TokenCookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            options.TokenCookie.SecurePolicy = hostingEnvironment.IsProduction()
                ? Microsoft.AspNetCore.Http.CookieSecurePolicy.Always
                : Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
        });
        
        Configure<Volo.Abp.MultiTenancy.AbpTenantResolveOptions>(options =>
        {
            options.TenantResolvers.Clear();
            options.TenantResolvers.Add(new Volo.Abp.MultiTenancy.CurrentUserTenantResolveContributor());
            options.TenantResolvers.Add(new TenantPathResolveContributor());
        });
        
        Configure<RazorPagesOptions>(options =>
        {
            options.Conventions.AddPageRoute("/LicenseManagement/Index", "/host/licenses");
            options.Conventions.AuthorizePage("/License/Unavailable");
            options.Conventions.AuthorizePage("/Dashboard/Index");
            options.Conventions.AuthorizeFolder("/Products", NoxStockPermissions.Products.Default);
            options.Conventions.AuthorizeFolder("/Brands", NoxStockPermissions.Brands.Default);
            options.Conventions.AuthorizeFolder("/Categories", NoxStockPermissions.Categories.Default);
            options.Conventions.AuthorizePage("/ProductMovements/Index", NoxStockPermissions.StockMovements.Default);
            options.Conventions.AuthorizePage("/PrintLabels/Index", NoxStockPermissions.PrintLabels.Default);
            options.Conventions.AuthorizeFolder("/Customers", NoxStockPermissions.BusinessPartners.Default);
            options.Conventions.AuthorizeFolder("/Suppliers", NoxStockPermissions.BusinessPartners.Default);
            options.Conventions.AuthorizePage("/CustomerDetail/Index", NoxStockPermissions.BusinessPartners.Default);
            options.Conventions.AuthorizePage("/Customers/SalesHistory", NoxStockPermissions.BusinessPartners.Default);
            options.Conventions.AuthorizePage("/Accounts/Index", NoxStockPermissions.Accounts.Default);
            options.Conventions.AuthorizePage("/Invoices/Index", NoxStockPermissions.Documents.Default);
            options.Conventions.AuthorizePage("/Offers/Index", NoxStockPermissions.Offers.Default);
            options.Conventions.AuthorizePage("/Orders/Index", NoxStockPermissions.Orders.Default);
            options.Conventions.AuthorizePage("/Sales/Index", NoxStockPermissions.Sales.Default);
            options.Conventions.AuthorizePage("/Reports/Index", NoxStockPermissions.Reports.Default);
            options.Conventions.AuthorizePage("/Notifications/Index", NoxStockPermissions.Notifications.Default);
            options.Conventions.AuthorizeFolder("/LicenseManagement", NoxStockPermissions.Licenses.Default);
            options.Conventions.AuthorizePage("/AuditLogs/Index", NoxStockPermissions.AuditLogs.Default);
            options.Conventions.AuthorizePage("/Settings/Index", NoxStockPermissions.Settings.Default);
            options.Conventions.AuthorizePage("/Personnel/Index", NoxStockPermissions.Personnel.Default);
        });
    }

    private void ConfigureHealthChecks(ServiceConfigurationContext context)
    {
        context.Services.AddNoxStockHealthChecks();
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
                    bundle.AddFiles("/frontend-metrics.js");
                    bundle.AddFiles("/currency-ticker.js");
                    bundle.AddFiles("/topbar-alerts.js");
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
            options =>
            {
                options.Cookie.Path = "/";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = context.Services.GetHostingEnvironment().IsProduction()
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.SameAsRequest;
                var previousLoginRedirect = options.Events.OnRedirectToLogin;
                var previousAccessDeniedRedirect = options.Events.OnRedirectToAccessDenied;

                options.Events.OnRedirectToLogin = redirectContext =>
                {
                    if (IsApiRequest(redirectContext.Request))
                    {
                        redirectContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return System.Threading.Tasks.Task.CompletedTask;
                    }

                    return previousLoginRedirect(redirectContext);
                };

                options.Events.OnRedirectToAccessDenied = redirectContext =>
                {
                    if (IsApiRequest(redirectContext.Request))
                    {
                        redirectContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return System.Threading.Tasks.Task.CompletedTask;
                    }

                    return previousAccessDeniedRedirect(redirectContext);
                };
            });
        context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
        {
            options.IsDynamicClaimsEnabled = true;
        });
    }

    private static bool IsApiRequest(Microsoft.AspNetCore.Http.HttpRequest request)
    {
        return request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
    }

    private void ConfigureVirtualFileSystem(IWebHostEnvironment hostingEnvironment)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<NoxStockWebModule>();

            if (hostingEnvironment.IsDevelopment())
            {
                var domainSharedPath = Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}NoxStock.Domain.Shared", Path.DirectorySeparatorChar));
                if (Directory.Exists(domainSharedPath))
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<NoxStockDomainSharedModule>(domainSharedPath);
                }

                var domainPath = Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}NoxStock.Domain", Path.DirectorySeparatorChar));
                if (Directory.Exists(domainPath))
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<NoxStockDomainModule>(domainPath);
                }

                var appContractsPath = Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}NoxStock.Application.Contracts", Path.DirectorySeparatorChar));
                if (Directory.Exists(appContractsPath))
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<NoxStockApplicationContractsModule>(appContractsPath);
                }

                var appPath = Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}NoxStock.Application", Path.DirectorySeparatorChar));
                if (Directory.Exists(appPath))
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<NoxStockApplicationModule>(appPath);
                }

                var httpApiPath = Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}src{0}NoxStock.HttpApi", Path.DirectorySeparatorChar));
                if (Directory.Exists(httpApiPath))
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<NoxStockHttpApiModule>(httpApiPath);
                }

                options.FileSets.ReplaceEmbeddedByPhysical<NoxStockWebModule>(hostingEnvironment.ContentRootPath);
            }
        });
    }

    private void ConfigureNavigationServices()
    {
        Configure<AbpNavigationOptions>(options =>
        {
            options.MenuContributors.Add(new NoxStockMenuContributor());
        });

        Configure<AbpToolbarOptions>(options =>
        {
            options.Contributors.Add(new NoxStockToolbarContributor());
        });
    }

    private void ConfigureAutoApiControllers()
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(NoxStockApplicationModule).Assembly);
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        app.UseForwardedHeaders();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseAbpRequestLocalization();
        app.UseMiddleware<ApiAuthorizationResponseMiddleware>();

        if (!env.IsDevelopment())
        {
            app.UseErrorPage();
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseCorrelationId();
        app.UseMiddleware<TenantPathMiddleware>();
        app.UseMiddleware<PerformanceResponseHeadersMiddleware>();
        app.UseMiddleware<NoxStockExceptionHandlingMiddleware>();
        app.UseRouting();
        app.MapAbpStaticAssets();
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
        app.UseMiddleware<TenantLicenseEnforcementMiddleware>();
        app.UseDynamicClaims();
        app.UseAuthorization();

        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints();
    }

}
