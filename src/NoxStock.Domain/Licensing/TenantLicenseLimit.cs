using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace NoxStock.Licensing;

public class TenantLicenseLimit : FullAuditedEntity<Guid>
{
    public Guid TenantLicenseId { get; protected set; }
    public string LimitName { get; protected set; } = string.Empty;
    public int? LimitValue { get; protected set; }

    protected TenantLicenseLimit()
    {
    }

    public TenantLicenseLimit(
        Guid id,
        Guid tenantLicenseId,
        string limitName,
        int? limitValue) : base(id)
    {
        TenantLicenseId = tenantLicenseId;
        LimitName = Check.NotNullOrWhiteSpace(limitName, nameof(limitName), 64).Trim();
        SetValue(limitValue);
    }

    public void SetValue(int? limitValue)
    {
        if (limitValue < 0)
        {
            throw new BusinessException("NoxStock:InvalidLicenseLimit");
        }

        LimitValue = limitValue;
    }
}
