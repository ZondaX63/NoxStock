using Volo.Abp.Modularity;

namespace NoxStock;

[DependsOn(
    typeof(NoxStockApplicationModule),
    typeof(NoxStockDomainTestModule)
)]
public class NoxStockApplicationTestModule : AbpModule
{

}
