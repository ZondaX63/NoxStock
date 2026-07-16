using NoxStock.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace NoxStock.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class NoxStockController : AbpControllerBase
{
    protected NoxStockController()
    {
        LocalizationResource = typeof(NoxStockResource);
    }
}
