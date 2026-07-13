using System;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace NexStock.EntityFrameworkCore;

public abstract class NexStockEntityFrameworkCoreTestBase : NexStockTestBase<NexStockEntityFrameworkCoreTestModule>
{
    private readonly IDisposable _tenantScope;

    protected NexStockEntityFrameworkCoreTestBase()
    {
        _tenantScope = ServiceProvider
            .GetRequiredService<ICurrentTenant>()
            .Change(NexStockTestConsts.TenantId, "NexStock Test Tenant");
    }

    public new void Dispose()
    {
        _tenantScope.Dispose();
        base.Dispose();
    }
}
