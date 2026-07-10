using NexStock.Samples;
using Xunit;

namespace NexStock.EntityFrameworkCore.Applications;

[Collection(NexStockTestConsts.CollectionDefinitionName)]
public class EfCoreSampleAppServiceTests : SampleAppServiceTests<NexStockEntityFrameworkCoreTestModule>
{

}
