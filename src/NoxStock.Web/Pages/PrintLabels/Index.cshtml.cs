using System.Threading.Tasks;
using NoxStock.Settings;
using Volo.Abp.Settings;

namespace NoxStock.Web.Pages.PrintLabels;

public class IndexModel : NoxStockPageModel
{
    public string DefaultLabelFormat { get; private set; } = "barcode";

    private readonly ISettingProvider _settingProvider;

    public IndexModel(ISettingProvider settingProvider)
    {
        _settingProvider = settingProvider;
    }

    public async Task OnGetAsync()
    {
        DefaultLabelFormat = await _settingProvider.GetOrNullAsync(NoxStockSettings.DefaultLabelFormat) ?? "barcode";
    }
}
