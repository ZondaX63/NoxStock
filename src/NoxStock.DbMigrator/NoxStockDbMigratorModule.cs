using NoxStock.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace NoxStock.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(NoxStockEntityFrameworkCoreModule),
    typeof(NoxStockApplicationContractsModule)
)]
public class NoxStockDbMigratorModule : AbpModule
{
}
