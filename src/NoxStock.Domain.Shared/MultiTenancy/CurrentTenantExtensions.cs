using System;
using Volo.Abp;
using Volo.Abp.MultiTenancy;

namespace NoxStock.MultiTenancy;

public static class CurrentTenantExtensions
{
    public static Guid RequireTenantId(this ICurrentTenant currentTenant)
    {
        return currentTenant.Id ?? throw new BusinessException("NoxStock:TenantRequired");
    }

    public static void RequireHost(this ICurrentTenant currentTenant)
    {
        if (currentTenant.Id.HasValue)
        {
            throw new BusinessException("NoxStock:HostOnly");
        }
    }
}
