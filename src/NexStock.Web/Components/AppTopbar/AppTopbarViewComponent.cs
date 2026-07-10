using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace NexStock.Web.Components.AppTopbar;

public class AppTopbarViewComponent : AbpViewComponent
{
    public virtual IViewComponentResult Invoke()
    {
        return View("~/Components/AppTopbar/Default.cshtml");
    }
}
