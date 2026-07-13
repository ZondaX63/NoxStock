using System;
using Volo.Abp;
using Volo.Abp.MultiTenancy;

namespace NexStock.MultiTenancy;

public static class CurrentTenantExtensions
{
    public static Guid RequireTenantId(this ICurrentTenant currentTenant)
    {
        return currentTenant.Id ?? throw new BusinessException("NexStock:TenantRequired");
    }
}
