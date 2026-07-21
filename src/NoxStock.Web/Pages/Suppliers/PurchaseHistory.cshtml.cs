using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NoxStock.Brands;
using NoxStock.Categories;
using NoxStock.CustomerSalesHistory;
using NoxStock.SupplierPurchaseHistory;
using Volo.Abp.Domain.Entities;

namespace NoxStock.Web.Pages.Suppliers;

public class PurchaseHistoryModel : NoxStockPageModel
{
    private readonly ISupplierPurchaseHistoryAppService _historyService;
    private readonly IBrandAppService _brandService;
    private readonly ICategoryAppService _categoryService;

    [BindProperty(SupportsGet = true)] public Guid SupplierId { get; set; }
    [BindProperty(SupportsGet = true)] public CustomerSalesHistoryPeriodPreset PeriodPreset { get; set; } = CustomerSalesHistoryPeriodPreset.ThisMonth;
    [BindProperty(SupportsGet = true)] public DateTime? StartDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? EndDate { get; set; }
    [BindProperty(SupportsGet = true)] public CustomerSalesHistoryDocumentType DocumentType { get; set; }
    [BindProperty(SupportsGet = true)] public string? ProductSearch { get; set; }
    [BindProperty(SupportsGet = true)] public string? DocumentNumber { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? BrandId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Currency { get; set; }
    [BindProperty(SupportsGet = true)] public int SkipCount { get; set; }

    public CustomerSalesHistoryDto History { get; private set; } = new();
    public List<BrandDto> Brands { get; private set; } = new();
    public List<CategoryDto> Categories { get; private set; } = new();

    public PurchaseHistoryModel(ISupplierPurchaseHistoryAppService historyService, IBrandAppService brandService, ICategoryAppService categoryService)
    { _historyService = historyService; _brandService = brandService; _categoryService = categoryService; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (CurrentUser.TenantId is null || SupplierId == Guid.Empty) return NotFound();
        try
        {
            History = await _historyService.GetSupplierPurchaseHistoryAsync(new SupplierPurchaseHistoryInput
            {
                SupplierId = SupplierId, PeriodPreset = PeriodPreset, StartDate = StartDate, EndDate = EndDate,
                DocumentType = DocumentType, ProductSearch = ProductSearch, DocumentNumber = DocumentNumber,
                BrandId = BrandId, CategoryId = CategoryId, Currency = Currency, SkipCount = SkipCount, MaxResultCount = 50
            });
        }
        catch (EntityNotFoundException) { return NotFound(); }

        var lookup = new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto { MaxResultCount = 250, Sorting = "Name" };
        Brands = (await _brandService.GetListAsync(lookup)).Items.ToList();
        Categories = (await _categoryService.GetListAsync(lookup)).Items.ToList();
        StartDate = History.StartDate; EndDate = History.EndDate; SkipCount = History.SkipCount;
        return Page();
    }

    public string Amount(decimal amount, string currency) => amount.ToString("N2", CultureInfo.CurrentCulture) + " " + currency;
    public string Quantity(decimal amount) => amount.ToString("0.##", CultureInfo.CurrentCulture);
}
