using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.Uow;

namespace NexStock.ExchangeRates;

public interface IExchangeRateUpdater
{
    Task<ExchangeRatesDto> RefreshAsync();
}

public sealed class ExchangeRateUpdater : IExchangeRateUpdater, ITransientDependency
{
    private static readonly string[] SupportedCurrencies = ["USD", "EUR"];
    private readonly IRepository<ExchangeRate, Guid> _exchangeRateRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public ExchangeRateUpdater(
        IRepository<ExchangeRate, Guid> exchangeRateRepository,
        IHttpClientFactory httpClientFactory,
        IGuidGenerator guidGenerator,
        IAsyncQueryableExecuter asyncExecuter,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _exchangeRateRepository = exchangeRateRepository;
        _httpClientFactory = httpClientFactory;
        _guidGenerator = guidGenerator;
        _asyncExecuter = asyncExecuter;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public async Task<ExchangeRatesDto> RefreshAsync()
    {
        var client = _httpClientFactory.CreateClient("TcmbExchangeRates");
        using var response = await client.GetAsync("kurlar/today.xml");
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync();
        var rates = ParseTcmbRates(xml);
        using var unitOfWork = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        await SaveRateAsync("USD", rates.UsdTry, rates.RateDate);
        await SaveRateAsync("EUR", rates.EurTry, rates.RateDate);
        await unitOfWork.CompleteAsync();

        return new ExchangeRatesDto
        {
            USD = CreateRateDto("USD", rates.UsdTry, rates.RateDate),
            EUR = CreateRateDto("EUR", rates.EurTry, rates.RateDate),
            LastUpdated = rates.RateDate
        };
    }

    private async Task SaveRateAsync(string currency, decimal rate, DateTime rateDate)
    {
        var queryable = await _exchangeRateRepository.GetQueryableAsync();
        var existing = await _asyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Currency == currency));

        if (existing is null)
        {
            await _exchangeRateRepository.InsertAsync(new ExchangeRate(_guidGenerator.Create(), currency, rate, rateDate));
            return;
        }

        existing.Rate = rate;
        existing.RateDate = rateDate;
        await _exchangeRateRepository.UpdateAsync(existing);
    }

    private async Task<List<ExchangeRate>> GetStoredRatesAsync()
    {
        var queryable = await _exchangeRateRepository.GetQueryableAsync();
        return await _asyncExecuter.ToListAsync(queryable.Where(x => SupportedCurrencies.Contains(x.Currency)));
    }

    internal static ExchangeRatesDto CreateResponse(IEnumerable<ExchangeRate> rates)
    {
        ExchangeRateDto? usd = null;
        ExchangeRateDto? eur = null;
        DateTime? lastUpdated = null;

        foreach (var rate in rates)
        {
            var dto = CreateRateDto(rate.Currency, rate.Rate, rate.RateDate);

            if (rate.Currency == "USD") usd = dto;
            if (rate.Currency == "EUR") eur = dto;
            if (!lastUpdated.HasValue || rate.RateDate > lastUpdated) lastUpdated = rate.RateDate;
        }

        return new ExchangeRatesDto { USD = usd, EUR = eur, LastUpdated = lastUpdated };
    }

    private static ExchangeRateDto CreateRateDto(string currency, decimal rate, DateTime rateDate)
    {
        return new ExchangeRateDto
        {
            Currency = currency,
            Rate = rate,
            Symbol = currency == "USD" ? "$" : "EUR",
            RateDate = rateDate
        };
    }

    internal static TcmbRates ParseTcmbRates(string xml)
    {
        var document = XDocument.Parse(xml);
        var root = document.Root ?? throw new InvalidOperationException("TCMB kur belgesi okunamadı.");
        var rateDate = ParseRateDate((string?)root.Attribute("Date"));
        var usdTry = GetForexSellingRate(root, "USD");
        var eurTry = GetForexSellingRate(root, "EUR");

        return new TcmbRates(usdTry, eurTry, rateDate);
    }

    private static decimal GetForexSellingRate(XElement root, string currency)
    {
        var currencyElement = root.Elements("Currency")
            .FirstOrDefault(x => string.Equals((string?)x.Attribute("CurrencyCode"), currency, StringComparison.OrdinalIgnoreCase));
        var unit = ParseDecimal(currencyElement?.Element("Unit")?.Value, currency + " birim");
        var rate = ParseDecimal(currencyElement?.Element("ForexSelling")?.Value, currency + " döviz satış kuru");

        if (unit <= 0 || rate <= 0)
        {
            throw new InvalidOperationException($"TCMB {currency} döviz satış kuru geçerli değil.");
        }

        return rate / unit;
    }

    private static DateTime ParseRateDate(string? value)
    {
        return DateTime.TryParseExact(value, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : DateTime.UtcNow;
    }

    private static decimal ParseDecimal(string? value, string field)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidOperationException($"TCMB yanıtındaki {field} okunamadı.");
        }

        return result;
    }

    internal sealed record TcmbRates(decimal UsdTry, decimal EurTry, DateTime RateDate);
}
