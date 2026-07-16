using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NoxStock.Brands;
using NoxStock.Categories;
using NoxStock.Products;
using NoxStock.Settings;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Web.Pages.Products;

public class IndexModel : NoxStockPageModel
{
    [BindProperty]
    public CreateUpdateProductDto Product { get; set; } = new();

    public List<SelectListItem> Brands { get; private set; } = new();
    public List<SelectListItem> Categories { get; private set; } = new();

    private readonly IProductAppService _productAppService;
    private readonly IBrandAppService _brandAppService;
    private readonly ICategoryAppService _categoryAppService;
    private readonly ICompanySettingsAppService _companySettingsAppService;

    public IndexModel(
        IProductAppService productAppService,
        IBrandAppService brandAppService,
        ICategoryAppService categoryAppService,
        ICompanySettingsAppService companySettingsAppService)
    {
        _productAppService = productAppService;
        _brandAppService = brandAppService;
        _categoryAppService = categoryAppService;
        _companySettingsAppService = companySettingsAppService;
    }

    public async Task OnGetAsync()
    {
        var settings = await _companySettingsAppService.GetAsync();
        Product = CreateDefaultProduct(settings.DefaultUnit, settings.DefaultCurrency);
        await LoadLookupsAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync();
            return Page();
        }

        await _productAppService.CreateAsync(Product);
        TempData["ProductCreated"] = $"{Product.Name} ürünü eklendi.";
        return RedirectToPage();
    }

    private async Task LoadLookupsAsync()
    {
        var request = new PagedAndSortedResultRequestDto { MaxResultCount = 1000, Sorting = "Name" };
        var brands = await _brandAppService.GetListAsync(request);
        var categories = await _categoryAppService.GetListAsync(request);

        Brands = brands.Items.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList();
        Categories = categories.Items.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList();
        Brands.Insert(0, new SelectListItem("Marka seçiniz", string.Empty));
        Categories.Insert(0, new SelectListItem("Kategori seçiniz", string.Empty));
    }

    private static CreateUpdateProductDto CreateDefaultProduct(string defaultUnit, string defaultCurrency)
    {
        return new CreateUpdateProductDto
        {
            Sku = $"STK-{DateTime.UtcNow:yyyyMMddHHmmss}",
            Unit = defaultUnit,
            Currency = defaultCurrency,
            PurchaseCurrency = defaultCurrency,
            SaleCurrency = defaultCurrency,
            TrackStock = true
        };
    }
}
