using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NexStock.Brands;
using NexStock.Categories;
using NexStock.Products;
using Volo.Abp.Application.Dtos;

namespace NexStock.Web.Pages.Products;

public class IndexModel : NexStockPageModel
{
    [BindProperty]
    public CreateUpdateProductDto Product { get; set; } = new();

    public List<SelectListItem> Brands { get; private set; } = new();
    public List<SelectListItem> Categories { get; private set; } = new();
    public List<ProductDto> Products { get; private set; } = new();

    private readonly IProductAppService _productAppService;
    private readonly IBrandAppService _brandAppService;
    private readonly ICategoryAppService _categoryAppService;

    public IndexModel(
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
        Product = CreateDefaultProduct();
        await LoadLookupsAsync();
        await LoadProductsAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync();
            await LoadProductsAsync();
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

    private async Task LoadProductsAsync()
    {
        var result = await _productAppService.GetListAsync(new PagedAndSortedResultRequestDto
        {
            MaxResultCount = 1000,
            Sorting = nameof(ProductDto.Name)
        });

        Products = result.Items.ToList();
    }

    private static CreateUpdateProductDto CreateDefaultProduct()
    {
        return new CreateUpdateProductDto
        {
            Sku = $"STK-{DateTime.UtcNow:yyyyMMddHHmmss}",
            Unit = "Adet",
            Currency = "TRY",
            PurchaseCurrency = "TRY",
            SaleCurrency = "TRY",
            TrackStock = true
        };
    }
}
