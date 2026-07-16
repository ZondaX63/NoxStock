using NoxStock.Samples;
using Xunit;

namespace NoxStock.EntityFrameworkCore.Applications;

[Collection(NoxStockTestConsts.CollectionDefinitionName)]
public class EfCoreSampleAppServiceTests : SampleAppServiceTests<NoxStockEntityFrameworkCoreTestModule>
{

}
