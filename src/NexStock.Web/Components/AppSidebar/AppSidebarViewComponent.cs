using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace NexStock.Web.Components.AppSidebar;

public class AppSidebarViewComponent : AbpViewComponent
{
    public virtual IViewComponentResult Invoke()
    {
        return View("~/Components/AppSidebar/Default.cshtml");
    }
}
