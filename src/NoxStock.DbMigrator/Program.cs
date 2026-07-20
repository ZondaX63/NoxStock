using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using System;

namespace NoxStock.DbMigrator;

class Program
{
    static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Volo.Abp", LogEventLevel.Warning)
#if DEBUG
                .MinimumLevel.Override("NoxStock", LogEventLevel.Debug)
#else
                .MinimumLevel.Override("NoxStock", LogEventLevel.Information)
#endif
                .Enrich.FromLogContext()
            .WriteTo.Async(c => c.File("Logs/logs.txt"))
            .WriteTo.Async(c => c.Console())
            .CreateLogger();

        await CreateHostBuilder(args).RunConsoleAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .AddAppSettingsSecretsJson()
            .ConfigureLogging((context, logging) => logging.ClearProviders())
            .ConfigureServices((hostContext, services) =>
            {
                if (hostContext.Configuration.GetValue<bool>("App:RequireProductionEnvironment") &&
                    !hostContext.HostingEnvironment.IsProduction())
                {
                    throw new InvalidOperationException(
                        "This deployment requires DOTNET_ENVIRONMENT=Production.");
                }

                if (hostContext.HostingEnvironment.IsProduction())
                {
                    var connectionString = hostContext.Configuration.GetConnectionString("Default");
                    if (string.IsNullOrWhiteSpace(connectionString) ||
                        connectionString.Contains("local-dev", StringComparison.OrdinalIgnoreCase) ||
                        connectionString.Contains("development-only", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            "ConnectionStrings:Default must be supplied from a production secret source.");
                    }

                    var adminPassword = hostContext.Configuration["Identity:AdminPassword"];
                    if (string.IsNullOrWhiteSpace(adminPassword) ||
                        adminPassword.Contains("Local-Admin", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            "Identity:AdminPassword must be supplied from a production secret source.");
                    }
                }

                services.AddHostedService<DbMigratorHostedService>();
            });
}
