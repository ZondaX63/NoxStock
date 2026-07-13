using Volo.Abp.Data;

namespace NexStock.MultiTenancy;

public static class NexStockTenantProperties
{
    public const string IsActive = "IsActive";

    public static bool IsNexStockActive(this IHasExtraProperties tenant)
    {
        return !tenant.ExtraProperties.TryGetValue(IsActive, out var value) ||
               value is not bool isActive ||
               isActive;
    }

    public static void SetNexStockActive(this IHasExtraProperties tenant, bool isActive)
    {
        tenant.SetProperty(IsActive, isActive);
    }
}
