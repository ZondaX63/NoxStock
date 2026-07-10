using NexStock.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace NexStock.Web.Pages;

public abstract class NexStockPageModel : AbpPageModel
{
    protected NexStockPageModel()
    {
        LocalizationResourceType = typeof(NexStockResource);
    }
}
