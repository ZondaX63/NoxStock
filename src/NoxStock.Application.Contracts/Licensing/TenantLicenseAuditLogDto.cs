using System;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Licensing;

public class TenantLicenseAuditLogDto : CreationAuditedEntityDto<Guid>
{
    public Guid TenantLicenseId { get; set; }
    public Guid TenantId { get; set; }
    public string Action { get; set; } = string.Empty;
    public LicenseStatus? PreviousStatus { get; set; }
    public LicenseStatus? NewStatus { get; set; }
    public string? Details { get; set; }
}
