using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NoxStock.Brands;
using NoxStock.Categories;
using NoxStock.Products;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Web.Pages.Products;

public class CreateModalModel : NoxStockPageModel
{
    [BindProperty]
    public CreateUpdateProductDto Product { get; set; } = new();

    public List<SelectListItem> Brands { get; set; } = new();
    public List<SelectListItem> Categories { get; set; } = new();

    private readonly IProductAppService _productAppService;
    private readonly IBrandAppService _brandAppService;
    private readonly ICategoryAppService _categoryAppService;

    public CreateModalModel(
        IProductAppService productAppService,
        IBrandAppService brandAppService,
        ICategoryAppService categoryAppService)
    {
        _productAppService = productAppService;
        _brandAppService = brandAppService;
        _categoryAppService = categoryAppService;
    }

    public async Task OnGetAsync()
    {
        Product = new CreateUpdateProductDto();
        
        var brandLookup = await _brandAppService.GetListAsync(new PagedAndSortedResultRequestDto { MaxResultCount = 1000 });
        Brands = brandLookup.Items.Select(b => new SelectListItem(b.Name, b.Id.ToString())).ToList();
        Brands.Insert(0, new SelectListItem("--- Select Brand ---", ""));

        var categoryLookup = await _categoryAppService.GetListAsync(new PagedAndSortedResultRequestDto { MaxResultCount = 1000 });
        Categories = categoryLookup.Items.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList();
        Categories.Insert(0, new SelectListItem("--- Select Category ---", ""));
    }

    public async Task<IActionResult> OnPostAsync()
    {
        NormalizeIntegralProductFields();
        await _productAppService.CreateAsync(Product);
        return NoContent();
    }

    private void NormalizeIntegralProductFields()
    {
        NormalizeInteger("Product.VatRate", value => Product.VatRate = Math.Clamp(value, 0, 100));
        NormalizeInteger("Product.PricingMode", value => Product.PricingMode = (ProductPricingMode)Math.Clamp(value, 0, 1));
    }

    private void NormalizeInteger(string key, Action<int> assign)
    {
        var raw = Request.Form[key].ToString();
        if (string.IsNullOrWhiteSpace(raw)) return;
        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            ModelState.Remove(key);
            assign((int)Math.Round(number, MidpointRounding.AwayFromZero));
        }
    }
}
