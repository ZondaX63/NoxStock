using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace NoxStock.Licensing;

public class TenantLicenseFeature : FullAuditedEntity<Guid>
{
    public Guid TenantLicenseId { get; protected set; }
    public string FeatureName { get; protected set; } = string.Empty;
    public bool IsEnabled { get; protected set; }

    protected TenantLicenseFeature()
    {
    }

    public TenantLicenseFeature(
        Guid id,
        Guid tenantLicenseId,
        string featureName,
        bool isEnabled) : base(id)
    {
        TenantLicenseId = tenantLicenseId;
        FeatureName = Check.NotNullOrWhiteSpace(featureName, nameof(featureName), 64).Trim();
        IsEnabled = isEnabled;
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }
}
