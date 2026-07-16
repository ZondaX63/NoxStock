using Volo.Abp.Modularity;

namespace NoxStock;

public abstract class NoxStockApplicationTestBase<TStartupModule> : NoxStockTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
