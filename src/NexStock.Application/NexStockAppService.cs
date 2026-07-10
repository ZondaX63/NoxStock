using NexStock.Localization;
using Volo.Abp.Application.Services;

namespace NexStock;

/* Inherit your application services from this class.
 */
public abstract class NexStockAppService : ApplicationService
{
    protected NexStockAppService()
    {
        LocalizationResource = typeof(NexStockResource);
    }
}
