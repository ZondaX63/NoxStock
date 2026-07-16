using Xunit;

namespace NoxStock.EntityFrameworkCore;

[CollectionDefinition(NoxStockTestConsts.CollectionDefinitionName)]
public class NoxStockEntityFrameworkCoreCollection : ICollectionFixture<NoxStockEntityFrameworkCoreFixture>
{

}
