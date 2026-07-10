using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NexStock.Brands;

namespace NexStock.Web.Pages.Brands;

public class CreateModalModel : NexStockPageModel
{
    [BindProperty]
    public CreateUpdateBrandDto Brand { get; set; } = new();

    private readonly IBrandAppService _brandAppService;

    public CreateModalModel(IBrandAppService brandAppService)
    {
        _brandAppService = brandAppService;
    }

    public void OnGet()
    {
        Brand = new CreateUpdateBrandDto();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _brandAppService.CreateAsync(Brand);
        return NoContent();
    }
}
