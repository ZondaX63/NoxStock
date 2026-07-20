using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.BusinessPartners;
using NoxStock.CustomerSalesHistory;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Xunit;

namespace NoxStock.EntityFrameworkCore.MultiTenancy;

[Collection(NoxStockTestConsts.CollectionDefinitionName)]
public class CustomerSalesHistoryTenantIsolationTests : NoxStockEntityFrameworkCoreTestBase, IAsyncLifetime
{
    private readonly TwoTenantTestContext _tenants;
    private readonly ICustomerSalesHistoryAppService _salesHistoryAppService;
    private readonly IBusinessPartnerAppService _businessPartnerAppService;

    public CustomerSalesHistoryTenantIsolationTests()
    {
        _tenants = new TwoTenantTestContext(
            ServiceProvider.GetRequiredService<ICurrentTenant>(),
            ServiceProvider.GetRequiredService<ITenantManager>(),
            ServiceProvider.GetRequiredService<ITenantRepository>(),
            ServiceProvider.GetRequiredService<IRepository<IdentityUser, Guid>>(),
            ServiceProvider.GetRequiredService<IGuidGenerator>());
        _salesHistoryAppService = ServiceProvider.GetRequiredService<ICustomerSalesHistoryAppService>();
        _businessPartnerAppService = ServiceProvider.GetRequiredService<IBusinessPartnerAppService>();
    }

    public Task InitializeAsync() => _tenants.InitializeAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Tenant_A_Cannot_Read_Tenant_B_Customer_Sales_History_And_Host_Is_Rejected()
    {
        BusinessPartnerDto tenantBCustomer;
        using (_tenants.ChangeToTenantB())
        {
            tenantBCustomer = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
            {
                Name = "Tenant B History Customer",
                Type = BusinessPartnerType.Customer,
                Currency = "TRY"
            });
        }

        using (_tenants.ChangeToTenantA())
        {
            await Should.ThrowAsync<EntityNotFoundException>(() => _salesHistoryAppService.GetCustomerSalesHistoryAsync(new CustomerSalesHistoryInput
            {
                CustomerId = tenantBCustomer.Id
            }));
        }

        using (_tenants.ChangeToHost())
        {
            var exception = await Should.ThrowAsync<BusinessException>(() => _salesHistoryAppService.GetCustomerSalesHistoryAsync(new CustomerSalesHistoryInput
            {
                CustomerId = tenantBCustomer.Id
            }));
            exception.Code.ShouldBe("NoxStock:TenantRequired");
        }
    }
}
