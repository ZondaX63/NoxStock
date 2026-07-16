using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace NoxStock.Web.MultiTenancy;

public sealed class TenantPathResolveContributor : TenantResolveContributorBase
{
    public override string Name => "NoxStockPath";

    public override Task ResolveAsync(ITenantResolveContext context)
    {
        var httpContext = context.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext;
        if (httpContext?.Items[TenantPathMiddleware.TenantNameItem] is string tenantName)
        {
            context.TenantIdOrName = tenantName;
            context.Handled = true;
        }

        return Task.CompletedTask;
    }
}
