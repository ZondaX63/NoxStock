using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoxStock.ExchangeRates;

namespace NoxStock.Web.ExchangeRates;

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
                var rates = await exchangeRateUpdater.RefreshAsync();
                _logger.LogInformation(
                    "TCMB döviz kurları güncellendi. USD/TRY: {UsdTry}, EUR/TRY: {EurTry}, KurTarihi: {RateDate}",
                    rates.USD?.Rate,
                    rates.EUR?.Rate,
                    rates.LastUpdated);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "TCMB döviz kurları yenilenemedi; son kayıtlı değerler kullanılacak.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
