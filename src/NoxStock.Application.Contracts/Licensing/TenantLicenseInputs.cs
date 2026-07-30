using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NoxStock.Licensing;

public class AssignTenantLicenseDto
{
    public Guid TenantId { get; set; }
    public Guid LicensePlanId { get; set; }
    public LicenseStatus Status { get; set; } = LicenseStatus.Trial;
    public DateTime StartsAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? GracePeriodEndsAtUtc { get; set; }

    [Required]
    [StringLength(100)]
    public string LicenseReference { get; set; } = string.Empty;

    public bool IsAutoRenew { get; set; }

    [StringLength(1000)]
    public string? InternalNote { get; set; }
}

public class ChangeTenantLicensePlanDto
{
    public Guid LicensePlanId { get; set; }

    [StringLength(1000)]
    public string? InternalNote { get; set; }
}

public class ExtendTenantLicenseDto
{
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? GracePeriodEndsAtUtc { get; set; }

    [StringLength(1000)]
    public string? InternalNote { get; set; }
}

public class SuspendTenantLicenseDto
{
    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class ChangeTenantLicenseStatusDto
{
    public LicenseStatus Status { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }
}

public class UpdateTenantLicenseEntitlementsDto
{
    public List<string> EnabledFeatures { get; set; } = [];

    [Range(1, int.MaxValue)]
    public int? MaxUsers { get; set; }

    [Range(1, int.MaxValue)]
    public int? MaxProducts { get; set; }

    [Range(1, int.MaxValue)]
    public int? MaxBranches { get; set; }
}
