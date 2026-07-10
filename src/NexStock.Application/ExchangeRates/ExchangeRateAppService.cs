using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace NexStock.ExchangeRates;

public class ExchangeRateAppService : ApplicationService, IExchangeRateAppService
{
    private static readonly string[] SupportedCurrencies = ["USD", "EUR"];
    private readonly IRepository<ExchangeRate, Guid> _exchangeRateRepository;
    private readonly IHttpClientFactory _httpClientFactory;

    public ExchangeRateAppService(
        IRepository<ExchangeRate, Guid> exchangeRateRepository,
        IHttpClientFactory httpClientFactory)
    {
        _exchangeRateRepository = exchangeRateRepository;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ExchangeRatesDto> GetCurrentAsync()
    {
        return CreateResponse(await GetStoredRatesAsync());
    }

    public async Task<ExchangeRatesDto> RefreshAsync()
    {
        var client = _httpClientFactory.CreateClient("ExchangeRateApi");
        var response = await client.GetFromJsonAsync<ExchangeRateApiResponse>("v4/latest/USD");

        if (response?.Rates is null ||
            !response.Rates.TryGetValue("TRY", out var usdTry) ||
            !response.Rates.TryGetValue("EUR", out var usdEur) ||
            usdTry <= 0 || usdEur <= 0)
        {
            throw new InvalidOperationException("Döviz sağlayıcısı geçerli USD/TRY ve EUR değerlerini döndürmedi.");
        }

        var rateDate = DateTime.UtcNow;
        await SaveRateAsync("USD", usdTry, rateDate);
        await SaveRateAsync("EUR", usdTry / usdEur, rateDate);

        return CreateResponse(await GetStoredRatesAsync());
    }

    private async Task SaveRateAsync(string currency, decimal rate, DateTime rateDate)
    {
        var queryable = await _exchangeRateRepository.GetQueryableAsync();
        var existing = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Currency == currency));

        if (existing is null)
        {
            await _exchangeRateRepository.InsertAsync(new ExchangeRate(GuidGenerator.Create(), currency, rate, rateDate));
            return;
        }

        existing.Rate = rate;
        existing.RateDate = rateDate;
        await _exchangeRateRepository.UpdateAsync(existing);
    }

    private async Task<List<ExchangeRate>> GetStoredRatesAsync()
    {
        var queryable = await _exchangeRateRepository.GetQueryableAsync();
        return await AsyncExecuter.ToListAsync(queryable.Where(x => SupportedCurrencies.Contains(x.Currency)));
    }

    private static ExchangeRatesDto CreateResponse(IEnumerable<ExchangeRate> rates)
    {
        ExchangeRateDto? usd = null;
        ExchangeRateDto? eur = null;
        DateTime? lastUpdated = null;

        foreach (var rate in rates)
        {
            var dto = new ExchangeRateDto
            {
                Currency = rate.Currency,
                Rate = rate.Rate,
                Symbol = rate.Currency == "USD" ? "$" : "EUR",
                RateDate = rate.RateDate
            };

            if (rate.Currency == "USD") usd = dto;
            if (rate.Currency == "EUR") eur = dto;
            if (!lastUpdated.HasValue || rate.RateDate > lastUpdated) lastUpdated = rate.RateDate;
        }

        return new ExchangeRatesDto { USD = usd, EUR = eur, LastUpdated = lastUpdated };
    }

    private sealed class ExchangeRateApiResponse
    {
        public Dictionary<string, decimal>? Rates { get; set; }
    }
}
