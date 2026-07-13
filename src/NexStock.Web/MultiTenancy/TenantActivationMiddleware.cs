using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NexStock.MultiTenancy;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace NexStock.Web.MultiTenancy;

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
        ITenantRepository tenantRepository)
    {
        if (currentTenant.Id.HasValue)
        {
            var tenant = await tenantRepository.FindAsync(currentTenant.Id.Value);
            if (tenant is not null)
            {
                await ContinueForTenantAsync(context, tenant);
                return;
            }
        }

        if (context.Items[TenantPathMiddleware.TenantNameItem] is string tenantName)
        {
            var tenant = await tenantRepository.FindByNameAsync(tenantName.ToUpperInvariant());
            if (tenant is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            using (currentTenant.Change(tenant.Id, tenant.Name))
            {
                await ContinueForTenantAsync(context, tenant);
            }
            return;
        }

        await _next(context);
    }

    private async Task ContinueForTenantAsync(HttpContext context, Tenant tenant)
    {
        if (!tenant.IsNexStockActive())
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Tenant is inactive.");
            return;
        }

        await _next(context);
    }
}
