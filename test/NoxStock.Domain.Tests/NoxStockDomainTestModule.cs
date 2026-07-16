using Volo.Abp.Modularity;

namespace NoxStock;

[DependsOn(
    typeof(NoxStockDomainModule),
    typeof(NoxStockTestBaseModule)
)]
public class NoxStockDomainTestModule : AbpModule
{

}
