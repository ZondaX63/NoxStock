using System;
using System.Collections.Generic;

namespace NoxStock.Licensing;

public class TenantLicenseAccessDto
{
    public Guid TenantId { get; set; }
    public Guid? TenantLicenseId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public LicenseStatus? Status { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? GracePeriodEndsAtUtc { get; set; }
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public Dictionary<string, bool> Features { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int?> Limits { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
