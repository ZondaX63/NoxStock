using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NoxStock.Notifications;

namespace NoxStock.Web.Pages.Notifications;

public class IndexModel : PageModel
{
    private readonly IOperationalAlertsAppService _alertsAppService;

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public OperationalAlertsPageDto Summary { get; private set; } = new();
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Summary.TotalCount / (double)Summary.MaxResultCount));

    public IndexModel(IOperationalAlertsAppService alertsAppService)
    {
        _alertsAppService = alertsAppService;
    }

    public async Task OnGetAsync()
    {
        PageNumber = Math.Clamp(PageNumber, 1, 501);
        const int pageSize = 20;
        Summary = await _alertsAppService.GetPageAsync(new OperationalAlertGetListInput
        {
            SkipCount = (PageNumber - 1) * pageSize,
            MaxResultCount = pageSize
        });

        if (PageNumber > TotalPages)
        {
            PageNumber = TotalPages;
            Summary = await _alertsAppService.GetPageAsync(new OperationalAlertGetListInput
            {
                SkipCount = (PageNumber - 1) * pageSize,
                MaxResultCount = pageSize
            });
        }
    }
}
