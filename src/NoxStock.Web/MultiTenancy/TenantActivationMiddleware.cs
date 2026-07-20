using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NoxStock.MultiTenancy;
using Volo.Abp.MultiTenancy;
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

            if (currentUser.IsAuthenticated && currentUser.TenantId != pathTenant.Id)
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
}
