using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Volo.Abp.MultiTenancy;

namespace NexStock.Web.MultiTenancy;

public sealed class PlatformAdministrationMiddleware
{
    private static readonly string[] HostOnlyPrefixes = ["/AuditLogs", "/Identity"];
    private readonly RequestDelegate _next;

    public PlatformAdministrationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentTenant currentTenant)
    {
        var fullPath = context.Request.PathBase.Add(context.Request.Path);
        var tenantRouteRequested = TenantPathMiddleware.TryExtractTenantPath(
            fullPath,
            out _,
            out _,
            out var tenantRelativePath);
        var requestedRoute = tenantRouteRequested ? tenantRelativePath : context.Request.Path;

        if ((tenantRouteRequested || currentTenant.Id.HasValue) && IsHostOnlyRoute(requestedRoute))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await _next(context);
    }

    private static bool IsHostOnlyRoute(PathString path)
    {
        return Array.Exists(
            HostOnlyPrefixes,
            prefix => path.StartsWithSegments(new PathString(prefix), StringComparison.OrdinalIgnoreCase));
    }
}
