using Volo.Abp.Modularity;

namespace NexStock;

/* Inherit from this class for your domain layer tests. */
public abstract class NexStockDomainTestBase<TStartupModule> : NexStockTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
