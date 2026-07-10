using NexStock.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace NexStock.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class NexStockController : AbpControllerBase
{
    protected NexStockController()
    {
        LocalizationResource = typeof(NexStockResource);
    }
}
