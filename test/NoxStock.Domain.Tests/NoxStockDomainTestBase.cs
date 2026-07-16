using Volo.Abp.Modularity;

namespace NoxStock;

/* Inherit from this class for your domain layer tests. */
public abstract class NoxStockDomainTestBase<TStartupModule> : NoxStockTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
