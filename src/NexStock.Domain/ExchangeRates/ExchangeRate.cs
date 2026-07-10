using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace NexStock.ExchangeRates;

public class ExchangeRate : FullAuditedAggregateRoot<Guid>
{
    public string Currency { get; set; } = null!;
    public decimal Rate { get; set; }
    public DateTime RateDate { get; set; }

    protected ExchangeRate()
    {
    }

    public ExchangeRate(Guid id, string currency, decimal rate, DateTime rateDate)
        : base(id)
    {
        Currency = currency;
        Rate = rate;
        RateDate = rateDate;
    }
}
