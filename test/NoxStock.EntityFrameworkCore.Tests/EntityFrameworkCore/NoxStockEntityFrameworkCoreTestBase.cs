using System;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace NoxStock.EntityFrameworkCore;

public abstract class NoxStockEntityFrameworkCoreTestBase : NoxStockTestBase<NoxStockEntityFrameworkCoreTestModule>
{
    private readonly IDisposable _tenantScope;

    protected NoxStockEntityFrameworkCoreTestBase()
    {
        _tenantScope = ServiceProvider
            .GetRequiredService<ICurrentTenant>()
            .Change(NoxStockTestConsts.TenantId, "NoxStock Test Tenant");
    }

    public new void Dispose()
    {
        _tenantScope.Dispose();
        base.Dispose();
    }
}
