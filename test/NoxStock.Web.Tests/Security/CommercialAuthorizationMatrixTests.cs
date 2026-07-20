using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.MultiTenancy;
using Shouldly;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Xunit;

namespace NoxStock.Security;

[Collection(NoxStockTestConsts.CollectionDefinitionName)]
public class CommercialAuthorizationMatrixTests : NoxStockWebTestBase
{
    [Theory]
    [InlineData("api/app/dashboard/summary")]
    [InlineData("api/app/product?maxResultCount=10&skipCount=0")]
    [InlineData("api/app/product-pricing/quote?productId=00000000-0000-0000-0000-000000000001&saleType=0")]
    [InlineData("api/app/brand?maxResultCount=10&skipCount=0")]
    [InlineData("api/app/category?maxResultCount=10&skipCount=0")]
    [InlineData("api/app/business-partner?maxResultCount=10&skipCount=0")]
    [InlineData("api/app/account?maxResultCount=10&skipCount=0")]
    [InlineData("api/app/account-transaction?maxResultCount=10&skipCount=0")]
    [InlineData("api/app/document?maxResultCount=10&skipCount=0")]
    [InlineData("api/app/pos-sale/products?filter=&maxResultCount=10")]
    [InlineData("api/app/reporting/snapshot")]
    [InlineData("api/app/operational-alerts/summary")]
    [InlineData("api/app/company-settings")]
    [InlineData("api/app/stock-movement?maxResultCount=10&skipCount=0")]
    [InlineData("api/app/label-product-lookup")]
    public async Task Anonymous_Commercial_Endpoints_Should_Return_Unauthorized(string endpoint)
    {
        var tenant = await CreateTenantAsync(isActive: true);

        var response = await Client.GetAsync($"/t/{tenant.Name}/{endpoint}");

        response.StatusCode.ShouldBe(
            HttpStatusCode.Unauthorized,
            $"Unexpected redirect target: {response.Headers.Location}");
    }

    [Fact]
    public async Task Host_User_Should_Not_Access_Tenant_Commercial_Endpoint()
    {
        var tenant = await CreateTenantAsync(isActive: true);
        using var request = CreateAuthenticatedRequest($"/t/{tenant.Name}/api/app/dashboard/summary", "host-admin");

        var response = await Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("api/app/dashboard/summary")]
    [InlineData("api/app/product?maxResultCount=10&skipCount=0")]
    public async Task Tenant_Admin_Should_Access_Own_Commercial_Endpoints(string endpoint)
    {
        var tenant = await CreateTenantAsync(isActive: true);
        using var request = CreateAuthenticatedRequest($"/t/{tenant.Name}/{endpoint}", "tenant-admin", tenant.Id);

        var response = await Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Tenant_User_Should_Not_Access_Another_Tenant_Path()
    {
        var tenantA = await CreateTenantAsync(isActive: true);
        var tenantB = await CreateTenantAsync(isActive: true);
        using var request = CreateAuthenticatedRequest(
            $"/t/{tenantB.Name}/api/app/dashboard/summary",
            "tenant-a-admin",
            tenantA.Id);

        var response = await Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Tenant_User_Should_Not_Access_Host_Administration()
    {
        var tenant = await CreateTenantAsync(isActive: true);
        using var request = CreateAuthenticatedRequest("/api/identity/users", "tenant-admin", tenant.Id);

        var response = await Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Inactive_Tenant_User_Should_Be_Forbidden()
    {
        var tenant = await CreateTenantAsync(isActive: false);
        using var request = CreateAuthenticatedRequest(
            $"/t/{tenant.Name}/api/app/dashboard/summary",
            "inactive-admin",
            tenant.Id);

        var response = await Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(string path, string userName, Guid? tenantId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        TestAuthentication.AuthenticateAs(request, userName, tenantId);
        return request;
    }

    private async Task<Tenant> CreateTenantAsync(bool isActive)
    {
        using var scope = Server.Services.CreateScope();
        var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var unitOfWork = unitOfWorkManager.Begin();
        var tenantManager = scope.ServiceProvider.GetRequiredService<ITenantManager>();
        var tenantRepository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
        var name = $"security-{Guid.NewGuid():N}"[..24];
        var tenant = await tenantManager.CreateAsync(name);
        tenant.SetNoxStockActive(isActive);
        await tenantRepository.InsertAsync(tenant, autoSave: true);
        await unitOfWork.CompleteAsync();
        return tenant;
    }
}
