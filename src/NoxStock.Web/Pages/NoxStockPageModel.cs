using NoxStock.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace NoxStock.Web.Pages;

public abstract class NoxStockPageModel : AbpPageModel
{
    protected NoxStockPageModel()
    {
        LocalizationResourceType = typeof(NoxStockResource);
    }
}
