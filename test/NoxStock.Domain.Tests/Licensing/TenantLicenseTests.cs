using System;
using NoxStock.Licensing;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace NoxStock.Domain.Tests.Licensing;

public class TenantLicenseTests
{
    [Fact]
    public void Should_Resolve_Grace_And_Expired_Status_From_Utc_Period()
    {
        var now = DateTime.UtcNow;
        var license = CreateLicense(
            now.AddDays(-30),
            now.AddDays(-1),
            now.AddDays(6));

        license.GetEffectiveStatus(now).ShouldBe(LicenseStatus.GracePeriod);
        license.GetEffectiveStatus(now.AddDays(7)).ShouldBe(LicenseStatus.Expired);
    }

    [Fact]
    public void Should_Require_Reason_When_Suspending()
    {
        var license = CreateLicense(
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(30),
            DateTime.UtcNow.AddDays(37));

        Should.Throw<ArgumentException>(() => license.Suspend(" "));
        license.Status.ShouldBe(LicenseStatus.Active);
    }

    [Fact]
    public void Should_Reactivate_Only_Suspended_License()
    {
        var license = CreateLicense(
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(30),
            DateTime.UtcNow.AddDays(37));

        Should.Throw<BusinessException>(() => license.Reactivate())
            .Code.ShouldBe("NoxStock:LicenseNotSuspended");

        license.Suspend("Payment review");
        license.Reactivate();
        license.Status.ShouldBe(LicenseStatus.Active);
        license.SuspensionReason.ShouldBeNull();
    }

    [Fact]
    public void Should_Reject_Invalid_License_Period()
    {
        var now = DateTime.UtcNow;

        Should.Throw<BusinessException>(() =>
            CreateLicense(now, now, now.AddDays(1)))
            .Code.ShouldBe("NoxStock:InvalidLicensePeriod");
    }

    [Fact]
    public void Cancelled_License_Should_Be_Terminal()
    {
        var now = DateTime.UtcNow;
        var license = CreateLicense(now, now.AddDays(30), now.AddDays(37));

        license.Cancel("Customer request");

        Should.Throw<BusinessException>(() => license.ChangeStatus(LicenseStatus.Active))
            .Code.ShouldBe("NoxStock:LicenseIsNotCurrent");
        Should.Throw<BusinessException>(() =>
            license.Extend(now.AddDays(60), now.AddDays(67), null))
            .Code.ShouldBe("NoxStock:LicenseIsNotCurrent");
    }

    [Fact]
    public void Generic_Status_Change_Should_Require_Suspension_Reason()
    {
        var now = DateTime.UtcNow;
        var license = CreateLicense(now, now.AddDays(30), now.AddDays(37));

        Should.Throw<ArgumentException>(() =>
            license.ChangeStatus(LicenseStatus.Suspended));
    }

    private static TenantLicense CreateLicense(
        DateTime startsAtUtc,
        DateTime expiresAtUtc,
        DateTime? gracePeriodEndsAtUtc)
    {
        return new TenantLicense(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            LicenseStatus.Active,
            startsAtUtc,
            expiresAtUtc,
            gracePeriodEndsAtUtc,
            $"TEST-{Guid.NewGuid():N}",
            false);
    }
}
