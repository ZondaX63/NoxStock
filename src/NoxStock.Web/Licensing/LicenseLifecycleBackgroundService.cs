using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoxStock.Licensing;

namespace NoxStock.Web.Licensing;

public sealed class LicenseLifecycleBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LicenseLifecycleBackgroundService> _logger;

    public LicenseLifecycleBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<LicenseLifecycleBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProcessSafelyAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessSafelyAsync(stoppingToken);
        }
    }

    private async Task ProcessSafelyAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ITenantLicenseLifecycleService>();
            await service.ProcessAsync();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Tenant license lifecycle processing failed.");
        }
    }
}
