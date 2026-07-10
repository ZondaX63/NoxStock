using NexStock.Samples;
using Xunit;

namespace NexStock.EntityFrameworkCore.Domains;

[Collection(NexStockTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<NexStockEntityFrameworkCoreTestModule>
{

}
