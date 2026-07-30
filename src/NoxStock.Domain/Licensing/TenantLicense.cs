using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace NoxStock.Licensing;

public class TenantLicense : FullAuditedAggregateRoot<Guid>
{
    public Guid TenantId { get; protected set; }
    public Guid LicensePlanId { get; protected set; }
    public LicenseStatus Status { get; protected set; }
    public DateTime StartsAtUtc { get; protected set; }
    public DateTime ExpiresAtUtc { get; protected set; }
    public DateTime? GracePeriodEndsAtUtc { get; protected set; }
    public string LicenseReference { get; protected set; } = string.Empty;
    public bool IsAutoRenew { get; protected set; }
    public string? InternalNote { get; protected set; }
    public string? SuspensionReason { get; protected set; }
    public bool IsCurrent { get; protected set; }

    protected TenantLicense()
    {
    }

    public TenantLicense(
        Guid id,
        Guid tenantId,
        Guid licensePlanId,
        LicenseStatus status,
        DateTime startsAtUtc,
        DateTime expiresAtUtc,
        DateTime? gracePeriodEndsAtUtc,
        string licenseReference,
        bool isAutoRenew,
        string? internalNote = null) : base(id)
    {
        TenantId = tenantId;
        LicensePlanId = licensePlanId;
        SetPeriod(startsAtUtc, expiresAtUtc, gracePeriodEndsAtUtc);
        LicenseReference = Check.NotNullOrWhiteSpace(
            licenseReference,
            nameof(licenseReference),
            100).Trim();
        IsAutoRenew = isAutoRenew;
        InternalNote = internalNote?.Trim();
        Status = status;
        IsCurrent = true;
    }

    public void ChangePlan(Guid licensePlanId, string? internalNote)
    {
        EnsureCurrent();
        LicensePlanId = licensePlanId;
        InternalNote = internalNote?.Trim();
    }

    public void Extend(DateTime expiresAtUtc, DateTime? gracePeriodEndsAtUtc, string? internalNote)
    {
        EnsureCurrent();
        SetPeriod(StartsAtUtc, expiresAtUtc, gracePeriodEndsAtUtc);
        InternalNote = internalNote?.Trim();

        if (Status is LicenseStatus.Expired or LicenseStatus.GracePeriod or LicenseStatus.PastDue)
        {
            Status = LicenseStatus.Active;
        }
    }

    public void Suspend(string reason)
    {
        EnsureCurrent();
        SuspensionReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 500).Trim();
        Status = LicenseStatus.Suspended;
    }

    public void Reactivate()
    {
        EnsureCurrent();
        if (Status is not LicenseStatus.Suspended)
        {
            throw new BusinessException("NoxStock:LicenseNotSuspended");
        }

        SuspensionReason = null;
        Status = LicenseStatus.Active;
    }

    public void Cancel(string? internalNote)
    {
        EnsureCurrent();
        Status = LicenseStatus.Cancelled;
        IsCurrent = false;
        InternalNote = internalNote?.Trim();
    }

    public void ChangeStatus(LicenseStatus status, string? reason = null)
    {
        EnsureCurrent();
        if (status == LicenseStatus.Suspended)
        {
            Suspend(reason ?? string.Empty);
            return;
        }

        if (status == LicenseStatus.Cancelled)
        {
            Cancel(reason);
            return;
        }

        Status = status;
        SuspensionReason = null;
    }

    public LicenseStatus GetEffectiveStatus(DateTime utcNow)
    {
        if (Status is LicenseStatus.Suspended or LicenseStatus.Cancelled or LicenseStatus.Expired or LicenseStatus.PastDue)
        {
            return Status;
        }

        if (utcNow < ExpiresAtUtc)
        {
            return Status;
        }

        if (GracePeriodEndsAtUtc.HasValue && utcNow < GracePeriodEndsAtUtc.Value)
        {
            return LicenseStatus.GracePeriod;
        }

        return LicenseStatus.Expired;
    }

    private void SetPeriod(DateTime startsAtUtc, DateTime expiresAtUtc, DateTime? gracePeriodEndsAtUtc)
    {
        startsAtUtc = EnsureUtc(startsAtUtc);
        expiresAtUtc = EnsureUtc(expiresAtUtc);
        gracePeriodEndsAtUtc = gracePeriodEndsAtUtc.HasValue
            ? EnsureUtc(gracePeriodEndsAtUtc.Value)
            : null;

        if (expiresAtUtc <= startsAtUtc)
        {
            throw new BusinessException("NoxStock:InvalidLicensePeriod");
        }

        if (gracePeriodEndsAtUtc.HasValue && gracePeriodEndsAtUtc <= expiresAtUtc)
        {
            throw new BusinessException("NoxStock:InvalidLicenseGracePeriod");
        }

        StartsAtUtc = startsAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        GracePeriodEndsAtUtc = gracePeriodEndsAtUtc;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }

    private void EnsureCurrent()
    {
        if (!IsCurrent || Status == LicenseStatus.Cancelled)
        {
            throw new BusinessException("NoxStock:LicenseIsNotCurrent");
        }
    }
}
