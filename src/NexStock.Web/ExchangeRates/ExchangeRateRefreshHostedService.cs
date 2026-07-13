using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexStock.ExchangeRates;

namespace NexStock.Web.ExchangeRates;

public class ExchangeRateRefreshHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExchangeRateRefreshHostedService> _logger;

    public ExchangeRateRefreshHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExchangeRateRefreshHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var exchangeRateUpdater = scope.ServiceProvider.GetRequiredService<IExchangeRateUpdater>();
                await exchangeRateUpdater.RefreshAsync();
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Döviz kurları yenilenemedi; son kayıtlı değerler kullanılacak.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
