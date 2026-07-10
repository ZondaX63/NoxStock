using Volo.Abp.Modularity;

namespace NexStock;

[DependsOn(
    typeof(NexStockApplicationModule),
    typeof(NexStockDomainTestModule)
)]
public class NexStockApplicationTestModule : AbpModule
{

}
