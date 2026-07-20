using System;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NoxStock.Web.HealthChecks;

public static class HealthChecksBuilderExtensions
{
    public static void AddNoxStockHealthChecks(this IServiceCollection services)
    {
        var healthChecksBuilder = services.AddHealthChecks();
        healthChecksBuilder.AddCheck("NoxStock Process Check", () => HealthCheckResult.Healthy(), tags: new[] { "live" });
        healthChecksBuilder.AddCheck<NoxStockDatabaseCheck>("NoxStock DbContext Check", tags: new[] { "database", "ready" });

        var configuration = services.GetConfiguration();
        var environment = services.GetHostingEnvironment();
        var detailedResponse = environment.IsDevelopment() &&
                               configuration.GetValue("App:DetailedHealthResponse", false);
        services.ConfigureHealthCheckEndpoint("/health-status", detailedResponse);
        services.ConfigureHealthCheckEndpoint("/health/live", detailedResponse, registration => registration.Tags.Contains("live"));
        services.ConfigureHealthCheckEndpoint("/health/ready", detailedResponse, registration => registration.Tags.Contains("ready"));

        var healthCheckUrl = configuration["App:HealthCheckUrl"];

        if (string.IsNullOrEmpty(healthCheckUrl))
        {
            healthCheckUrl = "/health-status";
        }

        if (!environment.IsDevelopment() && !configuration.GetValue<bool>("App:EnableHealthUi"))
        {
            return;
        }

        var healthChecksUiBuilder = services.AddHealthChecksUI(settings =>
        {
            settings.AddHealthCheckEndpoint("NoxStock Health Status", configuration["App:HealthUiCheckUrl"] ?? healthCheckUrl);
        });

        // Set your HealthCheck UI Storage here
        healthChecksUiBuilder.AddInMemoryStorage();

        services.AddHttpClient("health-checks-ui")
            .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

        services.MapHealthChecksUiEndpoints(options =>
        {
            options.UIPath = "/health-ui";
            options.ApiPath = "/health-api";
        });
    }

    private static IServiceCollection ConfigureHealthCheckEndpoint(
        this IServiceCollection services,
        string path,
        bool detailedResponse,
        Func<HealthCheckRegistration, bool>? predicate = null)
    {
        services.Configure<AbpEndpointRouterOptions>(options =>
        {
            options.EndpointConfigureActions.Add(endpointContext =>
            {
                endpointContext.Endpoints.MapHealthChecks(
                    new PathString(path.EnsureStartsWith('/')),
                    new HealthCheckOptions
                    {
                        Predicate = predicate ?? (_ => true),
                        ResponseWriter = detailedResponse
                            ? UIResponseWriter.WriteHealthCheckUIResponse
                            : static async (context, report) =>
                            {
                                context.Response.ContentType = "application/json";
                                await context.Response.WriteAsJsonAsync(new { status = report.Status.ToString() });
                            },
                        AllowCachingResponses = false,
                    });
            });
        });

        return services;
    }

    private static IServiceCollection MapHealthChecksUiEndpoints(this IServiceCollection services, Action<global::HealthChecks.UI.Configuration.Options>? setupOption = null)
    {
        services.Configure<AbpEndpointRouterOptions>(routerOptions =>
        {
            routerOptions.EndpointConfigureActions.Add(endpointContext =>
            {
                endpointContext.Endpoints.MapHealthChecksUI(setupOption);
            });
        });

        return services;
    }
}
