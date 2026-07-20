using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NoxStock.Web.Diagnostics;
using Shouldly;
using Xunit;

namespace NoxStock.Security;

public class ProductionReleaseConfigurationTests
{
    [Fact]
    public void Required_Production_Deployment_Should_Reject_Development_Environment()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["App:RequireProductionEnvironment"] = "true"
        });

        var exception = Should.Throw<InvalidOperationException>(() =>
            ProductionSecurityConfigurationValidator.Validate(configuration, new TestHostEnvironment("Development")));

        exception.Message.ShouldContain("ASPNETCORE_ENVIRONMENT=Production");
    }

    [Fact]
    public void Production_Should_Require_A_Trusted_Proxy()
    {
        var configuration = BuildConfiguration(ValidProductionPreamble());

        var exception = Should.Throw<InvalidOperationException>(() =>
            ProductionSecurityConfigurationValidator.Validate(configuration, new TestHostEnvironment("Production")));

        exception.Message.ShouldContain("Trusted reverse proxy");
    }

    [Fact]
    public void Production_Should_Reject_Unbounded_Forwarding_Chains()
    {
        var values = ValidProductionPreamble();
        values["Proxy:Enabled"] = "true";
        values["Proxy:KnownProxies"] = "10.0.0.10";
        values["Proxy:ForwardLimit"] = "4";
        var configuration = BuildConfiguration(values);

        var exception = Should.Throw<InvalidOperationException>(() =>
            ProductionSecurityConfigurationValidator.Validate(configuration, new TestHostEnvironment("Production")));

        exception.Message.ShouldContain("ForwardLimit");
    }

    private static Dictionary<string, string?> ValidProductionPreamble()
    {
        return new Dictionary<string, string?>
        {
            ["App:SelfUrl"] = "https://private-beta.example.test",
            ["AuthServer:Authority"] = "https://private-beta.example.test",
            ["AuthServer:RequireHttpsMetadata"] = "true",
            ["App:DisablePII"] = "true",
            ["App:EnableSwagger"] = "false",
            ["App:EnableHealthUi"] = "false",
            ["App:DetailedHealthResponse"] = "false"
        };
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "NoxStock.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

[Collection(NoxStockTestConsts.CollectionDefinitionName)]
public class ReleaseHealthEndpointTests : NoxStockWebTestBase
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Release_Health_Endpoints_Should_Be_Available(string path)
    {
        var response = await Client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
