using Volo.Abp.Settings;

namespace NexStock.Settings;

public class NexStockSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        //Define your own settings here. Example:
        //context.Add(new SettingDefinition(NexStockSettings.MySetting1));
    }
}
