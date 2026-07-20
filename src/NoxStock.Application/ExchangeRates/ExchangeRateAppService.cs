using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace NoxStock.ExchangeRates;

public class ExchangeRateAppService : ApplicationService, IExchangeRateAppService
{
    private static readonly string[] SupportedCurrencies = ["USD", "EUR"];
    private readonly IRepository<ExchangeRate, Guid> _exchangeRateRepository;
    private readonly IExchangeRateUpdater _exchangeRateUpdater;

    public ExchangeRateAppService(
        IRepository<ExchangeRate, Guid> exchangeRateRepository,
        IExchangeRateUpdater exchangeRateUpdater)
    {
        _exchangeRateRepository = exchangeRateRepository;
        _exchangeRateUpdater = exchangeRateUpdater;
    }

    // Exchange rates are global market data and contain no tenant or commercial records.
    [AllowAnonymous]
    public async Task<ExchangeRatesDto> GetCurrentAsync()
    {
        return ExchangeRateUpdater.CreateResponse(await GetStoredRatesAsync());
    }

    [Authorize(NoxStockPermissions.ExchangeRates.Refresh)]
    public async Task<ExchangeRatesDto> RefreshAsync()
    {
        return await _exchangeRateUpdater.RefreshAsync();
    }

    private async Task<List<ExchangeRate>> GetStoredRatesAsync()
    {
        var queryable = await _exchangeRateRepository.GetQueryableAsync();
        return await AsyncExecuter.ToListAsync(queryable.Where(x => SupportedCurrencies.Contains(x.Currency)));
    }

}
