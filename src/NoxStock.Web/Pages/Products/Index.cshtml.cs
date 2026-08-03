using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NoxStock.Brands;
using NoxStock.Categories;
using NoxStock.Products;
using NoxStock.Settings;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Web.Pages.Products;

[RequestSizeLimit(ProductImportInputDto.MaximumFileSize + 64 * 1024)]
public class IndexModel : NoxStockPageModel
{
    [BindProperty]
    public CreateUpdateProductDto Product { get; set; } = new();

    [BindProperty]
    public IFormFile? ImportFile { get; set; }

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

    public async Task<IActionResult> OnPostImportAsync()
    {
        if (ImportFile is null || ImportFile.Length == 0)
        {
            TempData["ProductImportError"] = "İçe aktarılacak Excel dosyasını seçin.";
            return RedirectToPage();
        }

        if (ImportFile.Length > ProductImportInputDto.MaximumFileSize)
        {
            TempData["ProductImportError"] = "Excel dosyası en fazla 10 MB olabilir.";
            return RedirectToPage();
        }

        try
        {
            await using var stream = new MemoryStream((int)ImportFile.Length);
            await ImportFile.CopyToAsync(stream);
            var result = await _productAppService.ImportAsync(new ProductImportInputDto
            {
                FileName = Path.GetFileName(ImportFile.FileName),
                Content = stream.ToArray()
            });

            TempData["ProductImportSummary"] =
                $"{result.CreatedCount} yeni ürün eklendi, {result.UpdatedCount} ürün güncellendi.";

            var details = new List<string>();
            if (result.DuplicateRowCount > 0) details.Add($"{result.DuplicateRowCount} yinelenen satır atlandı");
            if (result.CreatedBrandCount > 0) details.Add($"{result.CreatedBrandCount} marka oluşturuldu");
            if (result.CreatedCategoryCount > 0) details.Add($"{result.CreatedCategoryCount} kategori oluşturuldu");
            if (result.IgnoredScientificBarcodeCount > 0) details.Add($"{result.IgnoredScientificBarcodeCount} bozulmuş barkod yok sayıldı");
            if (result.SkippedRowCount > 0) details.Add($"{result.SkippedRowCount} geçersiz satır atlandı");
            TempData["ProductImportDetails"] = string.Join(" · ", details);
        }
        catch (BusinessException exception)
        {
            TempData["ProductImportError"] = $"Excel dosyası aktarılamadı: {exception.Message}";
        }

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
            // Company accounting currency can stay TRY. Product reference
            // pricing is EUR by default and remains separately selectable.
            PurchaseCurrency = "EUR",
            ProfitMarkupPercent = 30m,
            TrackStock = true
        };
    }
}
