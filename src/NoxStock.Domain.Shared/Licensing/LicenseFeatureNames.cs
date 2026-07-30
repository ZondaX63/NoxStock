namespace NoxStock.Licensing;

public static class LicenseFeatureNames
{
    public const string Pos = "Pos";
    public const string DynamicPricing = "DynamicPricing";
    public const string MultiCurrency = "MultiCurrency";
    public const string AdvancedReports = "AdvancedReports";
    public const string Export = "Export";

    public static readonly string[] All =
    [
        Pos,
        DynamicPricing,
        MultiCurrency,
        AdvancedReports,
        Export
    ];
}
