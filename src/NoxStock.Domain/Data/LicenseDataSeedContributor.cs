using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NoxStock.Licensing;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.TenantManagement;
using Volo.Abp.Timing;

namespace NoxStock.Data;

public class LicenseDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<LicensePlan, Guid> _planRepository;
    private readonly IRepository<TenantLicense, Guid> _licenseRepository;
    private readonly IRepository<TenantLicenseFeature, Guid> _featureRepository;
    private readonly IRepository<TenantLicenseLimit, Guid> _limitRepository;
    private readonly IRepository<TenantLicenseAuditLog, Guid> _auditRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;
    private readonly Volo.Abp.Guids.IGuidGenerator _guidGenerator;

    public LicenseDataSeedContributor(
        IRepository<LicensePlan, Guid> planRepository,
        IRepository<TenantLicense, Guid> licenseRepository,
        IRepository<TenantLicenseFeature, Guid> featureRepository,
        IRepository<TenantLicenseLimit, Guid> limitRepository,
        IRepository<TenantLicenseAuditLog, Guid> auditRepository,
        ITenantRepository tenantRepository,
        IConfiguration configuration,
        IClock clock,
        Volo.Abp.Guids.IGuidGenerator guidGenerator)
    {
        _planRepository = planRepository;
        _licenseRepository = licenseRepository;
        _featureRepository = featureRepository;
        _limitRepository = limitRepository;
        _auditRepository = auditRepository;
        _tenantRepository = tenantRepository;
        _configuration = configuration;
        _clock = clock;
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

        var trialDays = Math.Max(1, _configuration.GetValue("Licensing:DefaultTrialDays", 30));
        var graceDays = Math.Max(0, _configuration.GetValue("Licensing:DefaultGracePeriodDays", 7));
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

            var startsAtUtc = _clock.Now.ToUniversalTime();
            var expiresAtUtc = startsAtUtc.AddDays(trialDays);
            DateTime? graceEndsAtUtc = graceDays > 0
                ? expiresAtUtc.AddDays(graceDays)
                : null;
            var license = new TenantLicense(
                _guidGenerator.Create(),
                tenant.Id,
                pilot.Id,
                LicenseStatus.Trial,
                startsAtUtc,
                expiresAtUtc,
                graceEndsAtUtc,
                $"PILOT-{tenant.Id:N}",
                isAutoRenew: false,
                "Private beta başlangıç lisansı.");

            await _licenseRepository.InsertAsync(license, autoSave: true);
            await SeedPilotEntitlementsAsync(license.Id);
            await _auditRepository.InsertAsync(
                new TenantLicenseAuditLog(
                    _guidGenerator.Create(),
                    license.Id,
                    tenant.Id,
                    LicenseAuditActionNames.Created,
                    null,
                    LicenseStatus.Trial,
                    "Private beta başlangıç lisansı otomatik oluşturuldu."),
                autoSave: true);
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

    private async Task SeedPilotEntitlementsAsync(Guid tenantLicenseId)
    {
        foreach (var featureName in LicenseFeatureNames.All)
        {
            await _featureRepository.InsertAsync(
                new TenantLicenseFeature(
                    _guidGenerator.Create(),
                    tenantLicenseId,
                    featureName,
                    isEnabled: true));
        }

        await _limitRepository.InsertAsync(
            new TenantLicenseLimit(
                _guidGenerator.Create(),
                tenantLicenseId,
                LicenseLimitNames.MaxUsers,
                LicensePlanDefaults.PilotMaxUsers));
        await _limitRepository.InsertAsync(
            new TenantLicenseLimit(
                _guidGenerator.Create(),
                tenantLicenseId,
                LicenseLimitNames.MaxProducts,
                LicensePlanDefaults.PilotMaxProducts));
        await _limitRepository.InsertAsync(
            new TenantLicenseLimit(
                _guidGenerator.Create(),
                tenantLicenseId,
                LicenseLimitNames.MaxBranches,
                LicensePlanDefaults.PilotMaxBranches));
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
