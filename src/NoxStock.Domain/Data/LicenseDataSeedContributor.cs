using System;
using System.Threading.Tasks;
using NoxStock.Licensing;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.TenantManagement;

namespace NoxStock.Data;

public class LicenseDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<LicensePlan, Guid> _planRepository;
    private readonly IRepository<TenantLicense, Guid> _licenseRepository;
    private readonly IRepository<TenantLicenseFeature, Guid> _featureRepository;
    private readonly IRepository<TenantLicenseLimit, Guid> _limitRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IGuidGenerator _guidGenerator;

    public LicenseDataSeedContributor(
        IRepository<LicensePlan, Guid> planRepository,
        IRepository<TenantLicense, Guid> licenseRepository,
        IRepository<TenantLicenseFeature, Guid> featureRepository,
        IRepository<TenantLicenseLimit, Guid> limitRepository,
        ITenantRepository tenantRepository,
        IGuidGenerator guidGenerator)
    {
        _planRepository = planRepository;
        _licenseRepository = licenseRepository;
        _featureRepository = featureRepository;
        _limitRepository = limitRepository;
        _tenantRepository = tenantRepository;
        _guidGenerator = guidGenerator;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (context.TenantId.HasValue)
        {
            return;
        }

        var pilot = await EnsurePlanAsync(
            "PILOT",
            "Pilot",
            "Private beta müşterileri için kontrollü başlangıç planı.",
            isCustom: false);

        await EnsurePlanAsync(
            "CUSTOM",
            "Özel",
            "Müşteriye özel özellik ve limitlerin tanımlandığı plan.",
            isCustom: true);

        var tenants = await _tenantRepository.GetListAsync();

        foreach (var tenant in tenants)
        {
            var currentLicense = await _licenseRepository.FirstOrDefaultAsync(
                license => license.TenantId == tenant.Id && license.IsCurrent);
            if (currentLicense is not null)
            {
                if (currentLicense.LicensePlanId == pilot.Id)
                {
                    await EnsurePilotLimitsAsync(currentLicense.Id);
                }

                continue;
            }

            // New tenants are assigned their initial Pilot license explicitly by
            // the tenant-creation workflow. Do not recreate a license removed by
            // a host administrator during a later seed or migration execution.
        }
    }

    private async Task<LicensePlan> EnsurePlanAsync(
        string code,
        string name,
        string description,
        bool isCustom)
    {
        var existing = await _planRepository.FirstOrDefaultAsync(plan => plan.Code == code);
        if (existing is not null)
        {
            return existing;
        }

        return await _planRepository.InsertAsync(
            new LicensePlan(_guidGenerator.Create(), code, name, description, isCustom),
            autoSave: true);
    }

    private async Task EnsurePilotLimitsAsync(Guid tenantLicenseId)
    {
        await EnsureLimitAsync(
            tenantLicenseId,
            LicenseLimitNames.MaxUsers,
            LicensePlanDefaults.PilotMaxUsers);
        await EnsureLimitAsync(
            tenantLicenseId,
            LicenseLimitNames.MaxProducts,
            LicensePlanDefaults.PilotMaxProducts);
        await EnsureLimitAsync(
            tenantLicenseId,
            LicenseLimitNames.MaxBranches,
            LicensePlanDefaults.PilotMaxBranches);
    }

    private async Task EnsureLimitAsync(Guid tenantLicenseId, string name, int value)
    {
        var limit = await _limitRepository.FirstOrDefaultAsync(
            item => item.TenantLicenseId == tenantLicenseId && item.LimitName == name);
        if (limit is null)
        {
            await _limitRepository.InsertAsync(
                new TenantLicenseLimit(_guidGenerator.Create(), tenantLicenseId, name, value));
            return;
        }

        if (limit.LimitValue != value)
        {
            limit.SetValue(value);
            await _limitRepository.UpdateAsync(limit);
        }
    }
}
