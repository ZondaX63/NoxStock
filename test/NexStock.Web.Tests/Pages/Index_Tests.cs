using System.Net;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace NexStock.Pages;

[Collection(NexStockTestConsts.CollectionDefinitionName)]
public class Index_Tests : NexStockWebTestBase
{
    [Fact]
    public async Task Root_Page_Should_Redirect_To_Dashboard()
    {
        var response = await GetResponseAsync("/", HttpStatusCode.Found);

        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.OriginalString.ShouldBe("/Dashboard");
    }

    [Fact]
    public async Task Public_Registration_Page_Should_Be_Closed_By_Default()
    {
        var response = await Client.GetAsync("/Account/Register");
        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.OriginalString.ShouldBe("/Error?httpStatusCode=404");
    }
}
