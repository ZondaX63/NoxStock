using System;

namespace NexStock.ExchangeRates;

public class ExchangeRateDto
{
    public string Currency { get; set; } = null!;
    public decimal Rate { get; set; }
    public string Symbol { get; set; } = null!;
    public DateTime RateDate { get; set; }
}
