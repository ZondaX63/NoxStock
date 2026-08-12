using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NoxStock.MultiTenancy;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.TenantManagement;
using Volo.Abp.Users;

namespace NoxStock.Web.MultiTenancy;

public sealed class TenantActivationMiddleware
{
    private readonly RequestDelegate _next;

    public TenantActivationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentTenant currentTenant,
        ITenantRepository tenantRepository,
        ICurrentUser currentUser)
    {
        if (context.Items[TenantPathMiddleware.TenantNameItem] is string tenantName)
        {
            var pathTenant = await tenantRepository.FindByNameAsync(tenantName.ToUpperInvariant());
            if (pathTenant is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (context.User.Identity?.IsAuthenticated == true &&
                !HasMatchingTenantClaim(context, pathTenant.Id))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            using (currentTenant.Change(pathTenant.Id, pathTenant.Name))
            {
                await ContinueForTenantAsync(context, pathTenant);
            }
            return;
        }

        if (currentTenant.Id.HasValue)
        {
            var tenant = await tenantRepository.FindAsync(currentTenant.Id.Value);
            if (tenant is not null)
            {
                // A tenant claim must not make host URLs act as a tenant workspace.
                // Keep workspace routes canonical so that permissions, links and
                // browser navigation always retain the tenant URL prefix.
                if (currentUser.IsAuthenticated && ShouldRedirectToTenantPath(context.Request.Path))
                {
                    var destination = $"/t/{Uri.EscapeDataString(tenant.Name)}{context.Request.Path}{context.Request.QueryString}";
                    context.Response.Redirect(destination);
                    return;
                }

                await ContinueForTenantAsync(context, tenant);
                return;
            }
        }

        await _next(context);
    }

    private async Task ContinueForTenantAsync(HttpContext context, Tenant tenant)
    {
        if (!tenant.IsNoxStockActive())
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Tenant is inactive.");
            return;
        }

        await _next(context);
    }

    private static bool ShouldRedirectToTenantPath(PathString path)
    {
        // These host/public endpoints must remain reachable without adding a
        // tenant prefix. Host-only routes are subsequently rejected by the
        // platform middleware for tenant sessions.
        return !path.StartsWithSegments("/Account", System.StringComparison.OrdinalIgnoreCase) &&
               !path.StartsWithSegments("/admin", System.StringComparison.OrdinalIgnoreCase) &&
               !path.StartsWithSegments("/TenantManagement", System.StringComparison.OrdinalIgnoreCase) &&
               !path.StartsWithSegments("/LicenseManagement", System.StringComparison.OrdinalIgnoreCase) &&
               !path.StartsWithSegments("/host", System.StringComparison.OrdinalIgnoreCase) &&
               !path.StartsWithSegments("/health", System.StringComparison.OrdinalIgnoreCase) &&
               !path.Equals("/", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasMatchingTenantClaim(HttpContext context, Guid tenantId)
    {
        var tenantIdClaim = context.User.FindFirst(AbpClaimTypes.TenantId)?.Value;
        return Guid.TryParse(tenantIdClaim, out var authenticatedTenantId) &&
               authenticatedTenantId == tenantId;
    }
}
