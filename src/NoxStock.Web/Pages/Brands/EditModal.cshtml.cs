using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NoxStock.Brands;

namespace NoxStock.Web.Pages.Brands;

public class EditModalModel : NoxStockPageModel
{
    [HiddenInput]
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public CreateUpdateBrandDto Brand { get; set; } = new();

    private readonly IBrandAppService _brandAppService;

    public EditModalModel(IBrandAppService brandAppService)
    {
        _brandAppService = brandAppService;
    }

    public async Task OnGetAsync()
    {
        var brandDto = await _brandAppService.GetAsync(Id);
        Brand = ObjectMapper.Map<BrandDto, CreateUpdateBrandDto>(brandDto);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _brandAppService.UpdateAsync(Id, Brand);
        return NoContent();
    }
}
