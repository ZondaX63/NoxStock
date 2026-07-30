using System;

namespace NoxStock.Licensing;

public class HostTenantLicenseRowDto
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public bool TenantIsActive { get; set; }
    public TenantLicenseDto? License { get; set; }
}
