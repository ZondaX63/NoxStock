using Volo.Abp.Modularity;

namespace NexStock;

public abstract class NexStockApplicationTestBase<TStartupModule> : NexStockTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
