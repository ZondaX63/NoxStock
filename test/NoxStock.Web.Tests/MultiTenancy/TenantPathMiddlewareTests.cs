using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NoxStock.Web.MultiTenancy;
using Shouldly;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace NoxStock.MultiTenancy;

public class TenantPathMiddlewareTests
{
    [Theory]
    [InlineData("/t/acme/Account/Login", "acme", "/t/acme", "/Account/Login")]
    [InlineData("/T/demo-company/Products", "demo-company", "/T/demo-company", "/Products")]
    [InlineData("/t/acme", "acme", "/t/acme", "/")]
    public void Should_Extract_Trusted_Tenant_Path(
        string input,
        string expectedTenant,
        string expectedPrefix,
        string expectedRemainingPath)
    {
        var extracted = TenantPathMiddleware.TryExtractTenantPath(
            new PathString(input),
            out var tenantName,
            out var tenantPrefix,
            out var remainingPath);

        extracted.ShouldBeTrue();
        tenantName.ShouldBe(expectedTenant);
        tenantPrefix.Value.ShouldBe(expectedPrefix);
        remainingPath.Value.ShouldBe(expectedRemainingPath);
    }

    [Theory]
    [InlineData("/Products")]
    [InlineData("/t/a/Products")]
    [InlineData("/t/acme_unsafe/Products")]
    [InlineData("/t/%2Fhost/Products")]
    public void Should_Reject_Untrusted_Or_Missing_Tenant_Path(string input)
    {
        TenantPathMiddleware.TryExtractTenantPath(
            new PathString(input),
            out _,
            out _,
            out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Expose_Tenant_To_Resolver_And_Rewrite_Routing_Path()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/t/acme/Account/Login";
        string? observedTenant = null;
        PathString observedPathBase = default;
        PathString observedPath = default;
        var middleware = new TenantPathMiddleware(nextContext =>
        {
            observedTenant = nextContext.Items[TenantPathMiddleware.TenantNameItem] as string;
            observedPathBase = nextContext.Request.PathBase;
            observedPath = nextContext.Request.Path;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        observedTenant.ShouldBe("acme");
        observedPathBase.Value.ShouldBe("/t/acme");
        observedPath.Value.ShouldBe("/Account/Login");
    }
}

[Collection(NoxStockTestConsts.CollectionDefinitionName)]
public class TenantPathIntegrationTests : NoxStockWebTestBase
{
    [Fact]
    public void Application_Identity_Cookie_Should_Always_Use_Root_Path()
    {
        var options = Server.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        options.Cookie.Path.ShouldBe("/");
    }

    [Fact]
    public async Task Tenant_Login_Path_Should_Resolve_And_Render_Login_Page()
    {
        AssertResolverChain();
        var tenantName = $"path-login-{Guid.NewGuid():N}"[..24];
        await CreateTenantAsync(tenantName, isActive: true);

        var response = await Client.GetAsync($"/t/{tenantName}/Account/Login");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Inactive_Tenant_Should_Be_Blocked_Before_Login()
    {
        AssertResolverChain();
        var tenantName = $"inactive-{Guid.NewGuid():N}"[..24];
        await CreateTenantAsync(tenantName, isActive: false);
        await AssertActivationMiddlewareBlocksAsync(tenantName);

        var response = await Client.GetAsync($"/t/{tenantName}/Products");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("AuditLogs")]
    [InlineData("Identity/Users")]
    public async Task Tenant_Route_Is_Blocked_From_Platform_Administration(string route)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = $"/t/company-a/{route}";
        var nextCalled = false;
        var middleware = new PlatformAdministrationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        using var scope = Server.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        await middleware.InvokeAsync(context, currentTenant);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        nextCalled.ShouldBeFalse();
    }

    private async Task CreateTenantAsync(string tenantName, bool isActive)
    {
        Guid tenantId;
        using (var scope = Server.Services.CreateScope())
        {
            var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
            using var unitOfWork = unitOfWorkManager.Begin();
            var tenantManager = scope.ServiceProvider.GetRequiredService<ITenantManager>();
            var tenantRepository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
            var tenant = await tenantManager.CreateAsync(tenantName);
            tenant.SetNoxStockActive(isActive);
            await tenantRepository.InsertAsync(tenant, autoSave: true);
            tenantId = tenant.Id;
            await unitOfWork.CompleteAsync();
        }

        using (var verificationScope = Server.Services.CreateScope())
        {
            var unitOfWorkManager = verificationScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
            using var unitOfWork = unitOfWorkManager.Begin();
            var tenantRepository = verificationScope.ServiceProvider.GetRequiredService<ITenantRepository>();
            var savedTenant = await tenantRepository.GetAsync(tenantId);
            savedTenant.IsNoxStockActive().ShouldBe(isActive);
            await unitOfWork.CompleteAsync();
        }
    }

    private void AssertResolverChain()
    {
        var options = Server.Services.GetRequiredService<IOptions<AbpTenantResolveOptions>>().Value;
        options.TenantResolvers.Select(x => x.Name).ShouldBe(["CurrentUser", "NoxStockPath"]);
    }

    private async Task AssertActivationMiddlewareBlocksAsync(string tenantName)
    {
        using var scope = Server.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var tenantRepository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
        var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUser>();
        var context = new DefaultHttpContext();
        context.Items[TenantPathMiddleware.TenantNameItem] = tenantName;
        var nextCalled = false;
        var middleware = new TenantActivationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, currentTenant, tenantRepository, currentUser);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        nextCalled.ShouldBeFalse();
    }
}
