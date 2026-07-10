using NexStock.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace NexStock.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(NexStockEntityFrameworkCoreModule),
    typeof(NexStockApplicationContractsModule)
)]
public class NexStockDbMigratorModule : AbpModule
{
}
