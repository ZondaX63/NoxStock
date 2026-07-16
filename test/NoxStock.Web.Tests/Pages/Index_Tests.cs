using System.Net;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace NoxStock.Pages;

[Collection(NoxStockTestConsts.CollectionDefinitionName)]
public class Index_Tests : NoxStockWebTestBase
{
    [Fact]
    public async Task Root_Page_Should_Redirect_To_Dashboard()
    {
        var response = await GetResponseAsync("/", HttpStatusCode.Found);

        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.OriginalString.ShouldBe("/Dashboard");
    }

    [Fact]
    public async Task Public_Registration_Route_Should_Redirect_To_Login()
    {
        var response = await Client.GetAsync("/Account/Register");
        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.OriginalString.ShouldBe("/Account/Login");
    }

    [Fact]
    public async Task Language_Switch_Should_Accept_Culture_And_UiCulture_And_Return_To_Requested_Page()
    {
        var response = await Client.GetAsync("/Abp/Languages/Switch?culture=en&uiCulture=en&returnUrl=%2FAccount%2FLogin");

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.OriginalString.ShouldBe("/Account/Login");
        response.Headers.TryGetValues("Set-Cookie", out var cookieHeaders).ShouldBeTrue();
        cookieHeaders.ShouldContain(header => header.Contains(".AspNetCore.Culture=c%3Den%7Cuic%3Den"));
    }
}
