using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace NoxStock.Web.Diagnostics;

public sealed class ApiAuthorizationResponseMiddleware
{
    private readonly RequestDelegate _next;

    public ApiAuthorizationResponseMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (context.Response.HasStarted ||
            !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
            context.Response.StatusCode != StatusCodes.Status302Found ||
            !context.Response.Headers.TryGetValue("Location", out var location))
        {
            return;
        }

        var locationValue = location.ToString();
        if (locationValue.Contains("httpStatusCode=401", StringComparison.OrdinalIgnoreCase))
        {
            Normalize(context, StatusCodes.Status401Unauthorized);
        }
        else if (locationValue.Contains("httpStatusCode=403", StringComparison.OrdinalIgnoreCase))
        {
            Normalize(context, StatusCodes.Status403Forbidden);
        }
    }

    private static void Normalize(HttpContext context, int statusCode)
    {
        context.Response.Headers.Remove("Location");
        context.Response.StatusCode = statusCode;
    }
}
