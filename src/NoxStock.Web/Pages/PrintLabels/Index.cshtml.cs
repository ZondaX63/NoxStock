using System.Collections.Generic;
using System.Threading.Tasks;
using NoxStock.PrintLabels;
using NoxStock.Settings;
using Volo.Abp.Settings;

namespace NoxStock.Web.Pages.PrintLabels;

public class IndexModel : NoxStockPageModel
{
    public List<LabelProductDto> Products { get; private set; } = new();
    public string DefaultLabelFormat { get; private set; } = "barcode";

    private readonly ILabelProductLookupAppService _labelProductLookupAppService;
    private readonly ISettingProvider _settingProvider;

    public IndexModel(
        ILabelProductLookupAppService labelProductLookupAppService,
        ISettingProvider settingProvider)
    {
        _labelProductLookupAppService = labelProductLookupAppService;
        _settingProvider = settingProvider;
    }

    public async Task OnGetAsync()
    {
        Products = await _labelProductLookupAppService.GetListAsync();
        DefaultLabelFormat = await _settingProvider.GetOrNullAsync(NoxStockSettings.DefaultLabelFormat) ?? "barcode";
    }
}
