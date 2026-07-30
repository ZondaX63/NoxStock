namespace NoxStock.Licensing;

public enum LicenseStatus
{
    Trial = 0,
    Active = 1,
    GracePeriod = 2,
    PastDue = 3,
    Suspended = 4,
    Expired = 5,
    Cancelled = 6
}
