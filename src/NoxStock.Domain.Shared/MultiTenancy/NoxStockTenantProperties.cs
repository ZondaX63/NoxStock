using Volo.Abp.Data;

namespace NoxStock.MultiTenancy;

public static class NoxStockTenantProperties
{
    public const string IsActive = "IsActive";

    public static bool IsNoxStockActive(this IHasExtraProperties tenant)
    {
        return !tenant.ExtraProperties.TryGetValue(IsActive, out var value) ||
               value is not bool isActive ||
               isActive;
    }

    public static void SetNoxStockActive(this IHasExtraProperties tenant, bool isActive)
    {
        tenant.SetProperty(IsActive, isActive);
    }
}
