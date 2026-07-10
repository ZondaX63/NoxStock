using Xunit;

namespace NexStock.EntityFrameworkCore;

[CollectionDefinition(NexStockTestConsts.CollectionDefinitionName)]
public class NexStockEntityFrameworkCoreCollection : ICollectionFixture<NexStockEntityFrameworkCoreFixture>
{

}
