using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using NoxStock.Products;
using Volo.Abp.Application.Dtos;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NoxStock.Web.Pages.ProductMovements;

public class IndexModel : PageModel
{
    public List<SelectListItem> Products { get; private set; } = new();

    private readonly IProductAppService _productAppService;

    public IndexModel(IProductAppService productAppService)
    {
        _productAppService = productAppService;
    }

    public async Task OnGetAsync()
    {
        var lookup = await _productAppService.GetListAsync(new ProductGetListInput
        {
            MaxResultCount = 1000,
            Sorting = nameof(ProductDto.Name)
        });

        Products = lookup.Items
            .Select(x => new SelectListItem($"{x.Name} ({x.Sku})", x.Id.ToString()))
            .ToList();
        Products.Insert(0, new SelectListItem("Tüm ürünler", ""));
    }
}
