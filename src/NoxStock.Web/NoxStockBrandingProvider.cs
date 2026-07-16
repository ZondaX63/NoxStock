using Volo.Abp.Ui.Branding;
using Volo.Abp.DependencyInjection;
using Microsoft.Extensions.Localization;
using NoxStock.Localization;

namespace NoxStock.Web;

[Dependency(ReplaceServices = true)]
public class NoxStockBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<NoxStockResource> _localizer;

    public NoxStockBrandingProvider(IStringLocalizer<NoxStockResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}
