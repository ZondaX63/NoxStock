using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace NexStock.Web.Diagnostics;

public sealed class NexStockExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public NexStockExceptionHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ILogger<NexStockExceptionHandlingMiddleware> logger,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted && !context.Request.Path.Equals("/Error"))
        {
            var traceId = context.TraceIdentifier;
            logger.LogError(
                exception,
                "Unhandled request failure. TraceId: {TraceId}; TenantId: {TenantId}; UserId: {UserId}; Method: {Method}; Path: {Path}",
                traceId,
                currentTenant.Id,
                currentUser.Id,
                context.Request.Method,
                context.Request.Path);

            context.Response.Clear();
            context.Response.Headers["X-Correlation-Id"] = traceId;

            if (IsProgrammaticRequest(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(new
                {
                    title = "Beklenmeyen bir hata oluştu.",
                    status = StatusCodes.Status500InternalServerError,
                    traceId
                });
                return;
            }

            var errorPath = context.Request.PathBase.Add("/Error").Value;
            var location = $"{errorPath}?traceId={Uri.EscapeDataString(traceId)}";
            context.Response.Redirect(location);
        }
    }

    private static bool IsProgrammaticRequest(HttpRequest request)
    {
        if (request.Path.StartsWithSegments("/api") || request.Path.StartsWithSegments("/_diagnostics"))
        {
            return true;
        }

        if (string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }
}
