using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace NexStock.ExchangeRates;

public interface IExchangeRateAppService : IApplicationService
{
    Task<ExchangeRatesDto> GetCurrentAsync();
    Task<ExchangeRatesDto> RefreshAsync();
}
