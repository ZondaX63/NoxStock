namespace NoxStock.Licensing;

public static class LicenseErrorCodes
{
    public const string Required = "LICENSE_REQUIRED";
    public const string Expired = "LICENSE_EXPIRED";
    public const string Suspended = "LICENSE_SUSPENDED";
    public const string Cancelled = "LICENSE_CANCELLED";
    public const string PastDueReadOnly = "LICENSE_PAST_DUE_READ_ONLY";
    public const string LimitReached = "LICENSE_LIMIT_REACHED";
    public const string FeatureNotIncluded = "FEATURE_NOT_INCLUDED";
}
