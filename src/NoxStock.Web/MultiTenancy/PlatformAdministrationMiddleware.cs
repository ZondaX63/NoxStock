using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Volo.Abp.MultiTenancy;

namespace NoxStock.Web.MultiTenancy;

public sealed class PlatformAdministrationMiddleware
{
    private static readonly string[] HostOnlyPrefixes =
    [
        "/AuditLogs",
        "/Identity",
        "/TenantManagement",
        "/admin",
        "/LicenseManagement",
        "/host",
        "/api/identity",
        "/api/tenant-management",
        "/api/app/audit-log",
        "/api/app/host-license",
        "/api/app/host-tenant-user"
    ];
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

        if (currentTenant.Id.HasValue && IsAdminLoginRoute(requestedRoute))
        {
            // Authentication uses one application cookie. Visiting the platform
            // login while a tenant user is signed in must first clear that tenant
            // session; otherwise the current-user tenant resolver would keep the
            // admin login request inside the tenant context.
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            context.Response.Redirect("/admin/login");
            return;
        }

        if ((tenantRouteRequested || currentTenant.Id.HasValue) && IsHostOnlyRoute(requestedRoute))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (IsHostOnlyRoute(requestedRoute) &&
            !IsAdminLoginRoute(requestedRoute) &&
            !(context.User.Identity?.IsAuthenticated ?? false))
        {
            var returnUrl = Uri.EscapeDataString($"{fullPath}{context.Request.QueryString}");
            context.Response.Redirect($"/admin/login?returnUrl={returnUrl}");
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

    private static bool IsAdminLoginRoute(PathString path)
    {
        return path.Equals("/admin/login", StringComparison.OrdinalIgnoreCase);
    }
}
