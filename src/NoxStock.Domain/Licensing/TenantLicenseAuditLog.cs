using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace NoxStock.Licensing;

public class TenantLicenseAuditLog : CreationAuditedEntity<Guid>
{
    public Guid TenantLicenseId { get; protected set; }
    public Guid TenantId { get; protected set; }
    public string Action { get; protected set; } = string.Empty;
    public LicenseStatus? PreviousStatus { get; protected set; }
    public LicenseStatus? NewStatus { get; protected set; }
    public string? Details { get; protected set; }

    protected TenantLicenseAuditLog()
    {
    }

    public TenantLicenseAuditLog(
        Guid id,
        Guid tenantLicenseId,
        Guid tenantId,
        string action,
        LicenseStatus? previousStatus,
        LicenseStatus? newStatus,
        string? details) : base(id)
    {
        TenantLicenseId = tenantLicenseId;
        TenantId = tenantId;
        Action = Check.NotNullOrWhiteSpace(action, nameof(action), 64).Trim();
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        Details = details?.Trim();
    }
}
