using NoxStock.Samples;
using Xunit;

namespace NoxStock.EntityFrameworkCore.Domains;

[Collection(NoxStockTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<NoxStockEntityFrameworkCoreTestModule>
{

}
