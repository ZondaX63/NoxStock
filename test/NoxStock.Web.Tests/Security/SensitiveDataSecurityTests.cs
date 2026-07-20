using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NoxStock.Web.Diagnostics;
using Shouldly;
using Volo.Abp.Auditing;
using Xunit;

namespace NoxStock.Security;

[Collection(NoxStockTestConsts.CollectionDefinitionName)]
public class SensitiveDataSecurityTests : NoxStockWebTestBase
{
    [Fact]
    public void Sanitizer_Should_Remove_Password_Token_Connection_String_And_Query_Data()
    {
        const string password = "audit-secret-password";
        const string token = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjMifQ.signature";
        const string connectionSecret = "database-secret";
        var input = $"password={password}; token={token}; connectionString=Host=db;Password={connectionSecret}";

        var sanitized = SensitiveDataSanitizer.Sanitize(input);
        var sanitizedUrl = SensitiveDataSanitizer.SanitizeUrl($"/api/app/product?access_token={token}");

        sanitized.ShouldNotBeNull();
        sanitized!.ShouldNotContain(password);
        sanitized.ShouldNotContain(token);
        sanitized.ShouldNotContain(connectionSecret);
        sanitizedUrl.ShouldBe("/api/app/product");
    }

    [Fact]
    public void Audit_Contributor_Should_Sanitize_Exception_Payloads()
    {
        const string password = "exception-secret-password";
        var auditInfo = new AuditLogInfo
        {
            Url = $"/Account/Login?password={password}"
        };
        auditInfo.Exceptions.Add(new InvalidOperationException($"password={password}"));
        var contributor = new SensitiveDataAuditLogContributor();

        contributor.PostContribute(new AuditLogContributionContext(Server.Services, auditInfo));

        auditInfo.Url.ShouldBe("/Account/Login");
        auditInfo.Exceptions[0].ToString().ShouldNotContain(password);
    }

    [Fact]
    public void Auditing_Should_Not_Record_Action_Parameters_Or_Anonymous_Requests()
    {
        var options = Server.Services.GetRequiredService<IOptions<AbpAuditingOptions>>().Value;

        options.DisableLogActionInfo.ShouldBeTrue();
        options.IsEnabledForAnonymousUsers.ShouldBeFalse();
        options.Contributors.ShouldContain(x => x is SensitiveDataAuditLogContributor);
    }

    [Fact]
    public void Production_Profile_Should_Reject_Http_Public_Url()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>
            {
                ["App:SelfUrl"] = "http://public.example.test",
                ["AuthServer:Authority"] = "https://public.example.test",
                ["AuthServer:RequireHttpsMetadata"] = "true",
                ["App:DisablePII"] = "true"
            })
            .Build();

        Should.Throw<InvalidOperationException>(() =>
            ProductionSecurityConfigurationValidator.Validate(configuration, new ProductionEnvironment()));
    }

    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "NoxStock.Tests";
        public string ContentRootPath { get; set; } = "/tmp";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
