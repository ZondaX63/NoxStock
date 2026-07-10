using Volo.Abp.Modularity;

namespace NexStock;

[DependsOn(
    typeof(NexStockDomainModule),
    typeof(NexStockTestBaseModule)
)]
public class NexStockDomainTestModule : AbpModule
{

}
