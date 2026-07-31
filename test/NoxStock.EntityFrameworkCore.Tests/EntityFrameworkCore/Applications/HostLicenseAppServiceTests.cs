using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.Licensing;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace NoxStock.EntityFrameworkCore.Applications;

public class HostLicenseAppServiceTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly IHostLicenseAppService _licenseAppService;
    private readonly ITenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IHostTenantUserAppService _hostTenantUserAppService;

    public HostLicenseAppServiceTests()
    {
        _licenseAppService = ServiceProvider.GetRequiredService<IHostLicenseAppService>();
        _tenantManager = ServiceProvider.GetRequiredService<ITenantManager>();
        _tenantRepository = ServiceProvider.GetRequiredService<ITenantRepository>();
        _currentTenant = ServiceProvider.GetRequiredService<ICurrentTenant>();
        _hostTenantUserAppService = ServiceProvider.GetRequiredService<IHostTenantUserAppService>();
    }

    [Fact]
    public async Task Should_Assign_Suspend_Reactivate_And_Audit_License()
    {
        using (_currentTenant.Change(null))
        {
            var tenant = await CreateTenantAsync();
            var plan = (await _licenseAppService.GetPlansAsync())
                .Single(item => item.Code == "PILOT");
            var now = DateTime.UtcNow;

            var license = await _licenseAppService.AssignAsync(new AssignTenantLicenseDto
            {
                TenantId = tenant.Id,
                LicensePlanId = plan.Id,
                Status = LicenseStatus.Trial,
                StartsAtUtc = now,
                ExpiresAtUtc = now.AddDays(30),
                GracePeriodEndsAtUtc = now.AddDays(37),
                LicenseReference = $"TEST-{tenant.Id:N}"
            });

            license.Status.ShouldBe(LicenseStatus.Trial);
            license.Limits.Single(item => item.LimitName == LicenseLimitNames.MaxProducts)
                .LimitValue.ShouldBe(10_000);
            license.Limits.Single(item => item.LimitName == LicenseLimitNames.MaxUsers)
                .LimitValue.ShouldBe(5);

            license = await _licenseAppService.SuspendAsync(
                license.Id,
                new SuspendTenantLicenseDto { Reason = "Test suspension" });
            license.Status.ShouldBe(LicenseStatus.Suspended);

            license = await _licenseAppService.ReactivateAsync(license.Id);
            license.Status.ShouldBe(LicenseStatus.Active);

            var audit = await _licenseAppService.GetAuditLogAsync(license.Id);
            audit.Select(item => item.Action).ShouldContain(LicenseAuditActionNames.Created);
            audit.Select(item => item.Action).ShouldContain(LicenseAuditActionNames.Suspended);
            audit.Select(item => item.Action).ShouldContain(LicenseAuditActionNames.Reactivated);

            await Should.ThrowAsync<BusinessException>(() =>
                _licenseAppService.AssignAsync(new AssignTenantLicenseDto
                {
                    TenantId = tenant.Id,
                    LicensePlanId = plan.Id,
                    Status = LicenseStatus.Active,
                    StartsAtUtc = now,
                    ExpiresAtUtc = now.AddDays(60),
                    LicenseReference = $"SECOND-{tenant.Id:N}"
                }));
        }
    }

    [Fact]
    public async Task Should_Update_Custom_Entitlements_And_Audit_Them()
    {
        using (_currentTenant.Change(null))
        {
            var tenant = await CreateTenantAsync();
            var plan = (await _licenseAppService.GetPlansAsync())
                .Single(item => item.Code == "CUSTOM");
            var now = DateTime.UtcNow;
            var license = await _licenseAppService.AssignAsync(new AssignTenantLicenseDto
            {
                TenantId = tenant.Id,
                LicensePlanId = plan.Id,
                Status = LicenseStatus.Active,
                StartsAtUtc = now,
                ExpiresAtUtc = now.AddDays(90),
                LicenseReference = $"CUSTOM-{tenant.Id:N}"
            });

            license = await _licenseAppService.UpdateEntitlementsAsync(
                license.Id,
                new UpdateTenantLicenseEntitlementsDto
                {
                    EnabledFeatures =
                    [
                        LicenseFeatureNames.Pos,
                        LicenseFeatureNames.MultiCurrency,
                        LicenseFeatureNames.DynamicPricing
                    ],
                    MaxUsers = 20,
                    MaxProducts = 100_000,
                    MaxBranches = 3
                });

            license.Features.Single(item => item.FeatureName == LicenseFeatureNames.Pos)
                .IsEnabled.ShouldBeTrue();
            license.Features.Single(item => item.FeatureName == LicenseFeatureNames.AdvancedReports)
                .IsEnabled.ShouldBeFalse();
            license.Limits.Single(item => item.LimitName == LicenseLimitNames.MaxUsers)
                .LimitValue.ShouldBe(20);
            license.Limits.Single(item => item.LimitName == LicenseLimitNames.MaxProducts)
                .LimitValue.ShouldBe(100_000);

            var audit = await _licenseAppService.GetAuditLogAsync(license.Id);
            audit.Select(item => item.Action)
                .ShouldContain(LicenseAuditActionNames.EntitlementsUpdated);
        }
    }

    [Fact]
    public async Task Should_Soft_Delete_License_And_Allow_A_Replacement_Assignment()
    {
        using (_currentTenant.Change(null))
        {
            var tenant = await CreateTenantAsync();
            var plan = (await _licenseAppService.GetPlansAsync())
                .Single(item => item.Code == "PILOT");
            var now = DateTime.UtcNow;
            var license = await _licenseAppService.AssignAsync(new AssignTenantLicenseDto
            {
                TenantId = tenant.Id,
                LicensePlanId = plan.Id,
                Status = LicenseStatus.Active,
                StartsAtUtc = now,
                ExpiresAtUtc = now.AddDays(30),
                LicenseReference = $"REMOVE-{tenant.Id:N}"
            });

            await _licenseAppService.DeleteAsync(license.Id);

            (await _licenseAppService.GetOverviewAsync())
                .Single(row => row.TenantId == tenant.Id)
                .License.ShouldBeNull();
            (await ServiceProvider.GetRequiredService<IRepository<TenantLicenseFeature, Guid>>()
                .GetListAsync(item => item.TenantLicenseId == license.Id))
                .ShouldBeEmpty();
            (await ServiceProvider.GetRequiredService<IRepository<TenantLicenseLimit, Guid>>()
                .GetListAsync(item => item.TenantLicenseId == license.Id))
                .ShouldBeEmpty();

            var replacement = await _licenseAppService.AssignAsync(new AssignTenantLicenseDto
            {
                TenantId = tenant.Id,
                LicensePlanId = plan.Id,
                Status = LicenseStatus.Active,
                StartsAtUtc = now,
                ExpiresAtUtc = now.AddDays(60),
                LicenseReference = $"REPLACEMENT-{tenant.Id:N}"
            });
            replacement.TenantId.ShouldBe(tenant.Id);
        }
    }

    [Fact]
    public async Task Pilot_User_Limit_Should_Block_The_Sixth_User()
    {
        Tenant tenant;
        using (_currentTenant.Change(null))
        {
            tenant = await CreateTenantAsync();
            var plan = (await _licenseAppService.GetPlansAsync())
                .Single(item => item.Code == "PILOT");
            var now = DateTime.UtcNow;
            await _licenseAppService.AssignAsync(new AssignTenantLicenseDto
            {
                TenantId = tenant.Id,
                LicensePlanId = plan.Id,
                Status = LicenseStatus.Active,
                StartsAtUtc = now,
                ExpiresAtUtc = now.AddDays(30),
                LicenseReference = $"USER-LIMIT-{tenant.Id:N}"
            });
        }

        using (_currentTenant.Change(null))
        {
            for (var index = 1; index <= LicensePlanDefaults.PilotMaxUsers; index++)
            {
                await _hostTenantUserAppService.CreateAsync(new CreateHostTenantUserDto
                {
                    TenantId = tenant.Id,
                    UserName = $"pilot-user-{index}",
                    Email = $"pilot-user-{index}@noxstock.test",
                    Password = "Aa123!test"
                });
            }

            var exception = await Should.ThrowAsync<BusinessException>(() =>
                _hostTenantUserAppService.CreateAsync(new CreateHostTenantUserDto
                {
                    TenantId = tenant.Id,
                    UserName = "pilot-user-six",
                    Email = "pilot-user-six@noxstock.test",
                    Password = "Aa123!test"
                }));
            exception.Code.ShouldBe(LicenseErrorCodes.LimitReached);
            exception.Data["Limit"].ShouldBe(LicenseLimitNames.MaxUsers);
            exception.Data["LimitValue"].ShouldBe(LicensePlanDefaults.PilotMaxUsers);
        }
    }

    [Fact]
    public async Task Tenant_Context_Should_Not_Access_Host_License_Management()
    {
        await Should.ThrowAsync<AbpAuthorizationException>(() =>
            _licenseAppService.GetPlansAsync());
    }

    [Fact]
    public async Task Suspended_License_Should_Block_The_Next_Tenant_Write_Check()
    {
        Tenant tenant;
        TenantLicenseDto license;
        using (_currentTenant.Change(null))
        {
            tenant = await CreateTenantAsync();
            var plan = (await _licenseAppService.GetPlansAsync())
                .Single(item => item.Code == "PILOT");
            var now = DateTime.UtcNow;
            license = await _licenseAppService.AssignAsync(new AssignTenantLicenseDto
            {
                TenantId = tenant.Id,
                LicensePlanId = plan.Id,
                Status = LicenseStatus.Active,
                StartsAtUtc = now,
                ExpiresAtUtc = now.AddDays(30),
                GracePeriodEndsAtUtc = now.AddDays(37),
                LicenseReference = $"ENFORCEMENT-{tenant.Id:N}"
            });
        }

        var checker = CreateEnforcedAccessChecker();
        using (_currentTenant.Change(tenant.Id, tenant.Name))
        {
            var access = await checker.GetCurrentAsync();
            access.CanWrite.ShouldBeTrue();
            access.Features[LicenseFeatureNames.Pos].ShouldBeTrue();
            access.Limits[LicenseLimitNames.MaxProducts].ShouldBe(10_000);
        }

        using (_currentTenant.Change(null))
        {
            await _licenseAppService.SuspendAsync(
                license.Id,
                new SuspendTenantLicenseDto { Reason = "Security test" });

            using var verificationUnitOfWork = ServiceProvider
                .GetRequiredService<IUnitOfWorkManager>()
                .Begin(requiresNew: true, isTransactional: false);
            var persistedLicense = await ServiceProvider
                .GetRequiredService<IRepository<TenantLicense, Guid>>()
                .GetAsync(license.Id);
            persistedLicense.Status.ShouldBe(LicenseStatus.Suspended);
            await verificationUnitOfWork.CompleteAsync();
        }

        using (_currentTenant.Change(tenant.Id, tenant.Name))
        {
            (await checker.GetCurrentAsync()).CanRead.ShouldBeFalse();
            var exception = await Should.ThrowAsync<BusinessException>(
                () => checker.EnsureCanWriteAsync());
            exception.Code.ShouldBe(LicenseErrorCodes.Suspended);
        }
    }

    private TenantLicenseAccessChecker CreateEnforcedAccessChecker()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Licensing:EnforcementEnabled"] = "true"
            })
            .Build();

        return new TenantLicenseAccessChecker(
            ServiceProvider.GetRequiredService<IRepository<TenantLicense, Guid>>(),
            ServiceProvider.GetRequiredService<IRepository<LicensePlan, Guid>>(),
            ServiceProvider.GetRequiredService<IRepository<TenantLicenseFeature, Guid>>(),
            ServiceProvider.GetRequiredService<IRepository<TenantLicenseLimit, Guid>>(),
            ServiceProvider.GetRequiredService<IDistributedCache<TenantLicenseAccessCacheItem, Guid>>(),
            _currentTenant,
            ServiceProvider.GetRequiredService<IUnitOfWorkManager>(),
            ServiceProvider.GetRequiredService<IClock>(),
            configuration);
    }

    private async Task<Tenant> CreateTenantAsync()
    {
        var tenant = await _tenantManager.CreateAsync(
            $"license-test-{Guid.NewGuid():N}"[..28]);
        return await _tenantRepository.InsertAsync(tenant, autoSave: true);
    }
}
