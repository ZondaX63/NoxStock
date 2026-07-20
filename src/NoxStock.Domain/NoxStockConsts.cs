using Volo.Abp.Identity;

namespace NoxStock;

public static class NoxStockConsts
{
    public const string DbTablePrefix = "App";
    public const string? DbSchema = null;
    public const string AdminEmailDefaultValue = IdentityDataSeedContributor.AdminEmailDefaultValue;
    public const string LocalDevelopmentAdminPassword = "Aa123!";
}
