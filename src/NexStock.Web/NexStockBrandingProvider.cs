using Volo.Abp.Ui.Branding;
using Volo.Abp.DependencyInjection;
using Microsoft.Extensions.Localization;
using NexStock.Localization;

namespace NexStock.Web;

[Dependency(ReplaceServices = true)]
public class NexStockBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<NexStockResource> _localizer;

    public NexStockBrandingProvider(IStringLocalizer<NexStockResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}
