namespace NoxStock.Licensing;

public static class LicenseLimitNames
{
    public const string MaxUsers = "MaxUsers";
    public const string MaxProducts = "MaxProducts";
    public const string MaxBranches = "MaxBranches";

    public static readonly string[] All =
    [
        MaxUsers,
        MaxProducts,
        MaxBranches
    ];
}
