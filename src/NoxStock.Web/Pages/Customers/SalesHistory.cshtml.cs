using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NoxStock.Brands;
using NoxStock.Categories;
using NoxStock.CustomerSalesHistory;
using Volo.Abp.Domain.Entities;

namespace NoxStock.Web.Pages.Customers;

public class SalesHistoryModel : NoxStockPageModel
{
    private readonly ICustomerSalesHistoryAppService _salesHistoryAppService;
    private readonly IBrandAppService _brandAppService;
    private readonly ICategoryAppService _categoryAppService;

    [BindProperty(SupportsGet = true)]
    public Guid CustomerId { get; set; }

    [BindProperty(SupportsGet = true)]
    public CustomerSalesHistoryPeriodPreset PeriodPreset { get; set; } = CustomerSalesHistoryPeriodPreset.ThisMonth;

    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public CustomerSalesHistoryDocumentType DocumentType { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ProductSearch { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DocumentNumber { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? BrandId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? CategoryId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Currency { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool OnlyReturns { get; set; }

    [BindProperty(SupportsGet = true)]
    public int SkipCount { get; set; }

    public CustomerSalesHistoryDto History { get; private set; } = new();
    public List<BrandDto> Brands { get; private set; } = new();
    public List<CategoryDto> Categories { get; private set; } = new();

    public SalesHistoryModel(
        ICustomerSalesHistoryAppService salesHistoryAppService,
        IBrandAppService brandAppService,
        ICategoryAppService categoryAppService)
    {
        _salesHistoryAppService = salesHistoryAppService;
        _brandAppService = brandAppService;
        _categoryAppService = categoryAppService;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (CurrentUser.TenantId is null || CustomerId == Guid.Empty)
        {
            return NotFound();
        }

        try
        {
            History = await _salesHistoryAppService.GetCustomerSalesHistoryAsync(new CustomerSalesHistoryInput
            {
                CustomerId = CustomerId,
                PeriodPreset = PeriodPreset,
                StartDate = StartDate,
                EndDate = EndDate,
                DocumentType = DocumentType,
                ProductSearch = ProductSearch,
                DocumentNumber = DocumentNumber,
                BrandId = BrandId,
                CategoryId = CategoryId,
                Currency = Currency,
                OnlyReturns = OnlyReturns,
                SkipCount = SkipCount,
                MaxResultCount = 50
            });
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }

        var lookupRequest = new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto
        {
            MaxResultCount = 250,
            Sorting = "Name"
        };
        Brands = (await _brandAppService.GetListAsync(lookupRequest)).Items.ToList();
        Categories = (await _categoryAppService.GetListAsync(lookupRequest)).Items.ToList();
        StartDate = History.StartDate;
        EndDate = History.EndDate;
        SkipCount = History.SkipCount;
        return Page();
    }

    public string FormatAmount(decimal value, string currency)
    {
        return value.ToString("N2", CultureInfo.CurrentCulture) + " " + currency;
    }

    public string FormatQuantity(decimal value)
    {
        return value.ToString("0.##", CultureInfo.CurrentCulture);
    }

    public string FormatDateRange(DateTime startDate, DateTime endDate)
    {
        return startDate.Date == endDate.Date
            ? startDate.ToString("d MMMM yyyy", CultureInfo.CurrentCulture)
            : startDate.ToString("d MMMM", CultureInfo.CurrentCulture) + " - " + endDate.ToString("d MMMM yyyy", CultureInfo.CurrentCulture);
    }

    public string GetChartHeight(CustomerSalesHistoryChartPointDto point)
    {
        var chartCurrency = Currency?.Trim().ToUpperInvariant() ?? History.Customer.Currency;
        var value = point.Totals.FirstOrDefault(x => x.Currency == chartCurrency)?.Amount ?? 0;
        var maximum = History.Chart
            .Select(x => x.Totals.FirstOrDefault(total => total.Currency == chartCurrency)?.Amount ?? 0)
            .DefaultIfEmpty()
            .Max();

        if (value <= 0 || maximum <= 0)
        {
            return "0%";
        }

        return Math.Max(8m, Math.Round(value / maximum * 100m, 0)).ToString(CultureInfo.InvariantCulture) + "%";
    }

    public string FormatChartTotals(CustomerSalesHistoryChartPointDto point)
    {
        return point.Totals.Count == 0
            ? "-"
            : string.Join(" | ", point.Totals.Select(total => FormatAmount(total.Amount, total.Currency)));
    }
}
