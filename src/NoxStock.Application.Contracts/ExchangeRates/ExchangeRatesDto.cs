using System;

namespace NoxStock.ExchangeRates;

public class ExchangeRatesDto
{
    public ExchangeRateDto? USD { get; set; }
    public ExchangeRateDto? EUR { get; set; }
    public DateTime? LastUpdated { get; set; }
}
