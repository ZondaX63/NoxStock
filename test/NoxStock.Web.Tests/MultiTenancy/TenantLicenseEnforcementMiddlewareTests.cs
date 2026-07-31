using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NoxStock.Licensing;
using NoxStock.Localization;
using NoxStock.Web.MultiTenancy;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace NoxStock.MultiTenancy;

[Collection(NoxStockTestConsts.CollectionDefinitionName)]
public class TenantLicenseEnforcementMiddlewareTests : NoxStockWebTestBase
{
    [Fact]
    public async Task Should_Redirect_Unlicensed_Tenant_From_Workspace_Page()
    {
        using var scope = Server.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IIdentityUserRepository>();
        var localizer = scope.ServiceProvider.GetRequiredService<IStringLocalizer<NoxStockResource>>();
        var tenantId = Guid.NewGuid();
        var nextCalled = false;
        var context = CreateAuthenticatedContext("/Products", "/t/acme");
        var middleware = new TenantLicenseEnforcementMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        using (currentTenant.Change(tenantId))
        {
            await middleware.InvokeAsync(
                context,
                currentTenant,
                new FixedLicenseAccessChecker(canRead: false),
                userRepository,
                localizer);
        }

        context.Response.StatusCode.ShouldBe(StatusCodes.Status302Found);
        context.Response.Headers.Location.ToString().ShouldBe("/t/acme/License/Unavailable");
        nextCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Reject_Unlicensed_Tenant_Api_Request()
    {
        using var scope = Server.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IIdentityUserRepository>();
        var localizer = scope.ServiceProvider.GetRequiredService<IStringLocalizer<NoxStockResource>>();
        var nextCalled = false;
        var context = CreateAuthenticatedContext("/api/app/product", "/t/acme");
        var middleware = new TenantLicenseEnforcementMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        using (currentTenant.Change(Guid.NewGuid()))
        {
            await middleware.InvokeAsync(
                context,
                currentTenant,
                new FixedLicenseAccessChecker(canRead: false),
                userRepository,
                localizer);
        }

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        context.Response.ContentType.ShouldStartWith("application/problem+json");
        nextCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Allow_License_Unavailable_Page()
    {
        using var scope = Server.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IIdentityUserRepository>();
        var localizer = scope.ServiceProvider.GetRequiredService<IStringLocalizer<NoxStockResource>>();
        var nextCalled = false;
        var context = CreateAuthenticatedContext("/License/Unavailable", "/t/acme");
        var middleware = new TenantLicenseEnforcementMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        using (currentTenant.Change(Guid.NewGuid()))
        {
            await middleware.InvokeAsync(
                context,
                currentTenant,
                new FixedLicenseAccessChecker(canRead: false),
                userRepository,
                localizer);
        }

        nextCalled.ShouldBeTrue();
    }

    private static DefaultHttpContext CreateAuthenticatedContext(string path, string pathBase)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.PathBase = pathBase;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
            "Test"));
        return context;
    }

    private sealed class FixedLicenseAccessChecker : ITenantLicenseAccessChecker
    {
        private readonly TenantLicenseAccessDto _access;

        public FixedLicenseAccessChecker(bool canRead)
        {
            _access = new TenantLicenseAccessDto
            {
                CanRead = canRead,
                CanWrite = canRead,
                Status = canRead ? LicenseStatus.Active : null
            };
        }

        public Task<TenantLicenseAccessDto> GetAsync(Guid tenantId) => Task.FromResult(_access);
        public Task<TenantLicenseAccessDto> GetCurrentAsync() => Task.FromResult(_access);
        public Task EnsureCanWriteAsync() => Task.CompletedTask;
        public Task EnsureCanWriteAsync(Guid tenantId) => Task.CompletedTask;
        public Task EnsureFeatureAsync(string featureName) => Task.CompletedTask;
        public Task EnsureLimitAsync(string limitName, long currentCount) => Task.CompletedTask;
        public Task RestrictAsync(Guid tenantId, LicenseStatus status) => Task.CompletedTask;
        public Task InvalidateAsync(Guid tenantId) => Task.CompletedTask;
    }
}
