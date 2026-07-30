using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Licensing;

public class TenantLicenseDto : FullAuditedEntityDto<Guid>
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public Guid LicensePlanId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public LicenseStatus Status { get; set; }
    public LicenseStatus EffectiveStatus { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? GracePeriodEndsAtUtc { get; set; }
    public string LicenseReference { get; set; } = string.Empty;
    public bool IsAutoRenew { get; set; }
    public string? InternalNote { get; set; }
    public string? SuspensionReason { get; set; }
    public List<TenantLicenseFeatureDto> Features { get; set; } = [];
    public List<TenantLicenseLimitDto> Limits { get; set; } = [];
}

public class TenantLicenseFeatureDto
{
    public string FeatureName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}

public class TenantLicenseLimitDto
{
    public string LimitName { get; set; } = string.Empty;
    public int? LimitValue { get; set; }
}
