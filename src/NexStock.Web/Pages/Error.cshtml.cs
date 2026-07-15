using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NexStock.Web.Pages;

public class ErrorModel : PageModel
{
    public string TraceId { get; private set; } = string.Empty;

    public void OnGet(string? traceId)
    {
        TraceId = string.IsNullOrWhiteSpace(traceId) ? HttpContext.TraceIdentifier : traceId;
    }
}
