using NoxStock.Localization;
using Volo.Abp.Application.Services;

namespace NoxStock;

/* Inherit your application services from this class.
 */
public abstract class NoxStockAppService : ApplicationService
{
    protected NoxStockAppService()
    {
        LocalizationResource = typeof(NoxStockResource);
    }
}
