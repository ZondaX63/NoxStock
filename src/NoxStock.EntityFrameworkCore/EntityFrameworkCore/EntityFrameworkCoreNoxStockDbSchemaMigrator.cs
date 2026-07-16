using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.Data;
using Volo.Abp.DependencyInjection;

namespace NoxStock.EntityFrameworkCore;

public class EntityFrameworkCoreNoxStockDbSchemaMigrator
    : INoxStockDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public EntityFrameworkCoreNoxStockDbSchemaMigrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        /* We intentionally resolving the NoxStockDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */

        await _serviceProvider
            .GetRequiredService<NoxStockDbContext>()
            .Database
            .MigrateAsync();
    }
}
