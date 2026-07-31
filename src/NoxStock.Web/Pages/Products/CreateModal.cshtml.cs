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
        NormalizeTryExchangeRate();
        
        var brandLookup = await _brandAppService.GetListAsync(new PagedAndSortedResultRequestDto { MaxResultCount = 1000 });
        Brands = brandLookup.Items.Select(b => new SelectListItem(b.Name, b.Id.ToString())).ToList();
        Brands.Insert(0, new SelectListItem("--- Select Brand ---", ""));

        var categoryLookup = await _categoryAppService.GetListAsync(new PagedAndSortedResultRequestDto { MaxResultCount = 1000 });
        Categories = categoryLookup.Items.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList();
        Categories.Insert(0, new SelectListItem("--- Select Category ---", ""));
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Browser number controls submit values with an invariant decimal
        // point. Normalize them before ABP validation runs under tr-TR.
        NormalizeDecimalProductFields();
        NormalizeIntegralProductFields();
        NormalizeTryExchangeRate();
        await _productAppService.CreateAsync(Product);
        return NoContent();
    }

    private void NormalizeTryExchangeRate()
    {
        if (string.Equals(Product.PurchaseCurrency, "TRY", StringComparison.OrdinalIgnoreCase) &&
            Product.ExchangeRateAtPurchase <= 0)
        {
            Product.ExchangeRateAtPurchase = 1m;
            ModelState.Remove("Product.ExchangeRateAtPurchase");
        }
    }

    private void NormalizeIntegralProductFields()
    {
        NormalizeInteger("Product.VatRate", value => Product.VatRate = Math.Clamp(value, 0, 100));
        NormalizeInteger("Product.PricingMode", value => Product.PricingMode = (ProductPricingMode)Math.Clamp(value, 0, 1));
    }

    private void NormalizeDecimalProductFields()
    {
        NormalizeDecimal("Product.PurchasePrice", value => Product.PurchasePrice = value);
        NormalizeDecimal("Product.PurchaseListPrice", value => Product.PurchaseListPrice = value);
        NormalizeDecimal("Product.PurchaseDiscount1", value => Product.PurchaseDiscount1 = value);
        NormalizeDecimal("Product.PurchaseDiscount2", value => Product.PurchaseDiscount2 = value);
        NormalizeDecimal("Product.PurchaseDiscount3", value => Product.PurchaseDiscount3 = value);
        NormalizeDecimal("Product.PurchaseDiscount4", value => Product.PurchaseDiscount4 = value);
        NormalizeDecimal("Product.ExchangeRateAtPurchase", value => Product.ExchangeRateAtPurchase = value);
        NormalizeDecimal("Product.SalePrice", value => Product.SalePrice = value);
        NormalizeDecimal("Product.ProfitMarkupPercent", value => Product.ProfitMarkupPercent = value);
        NormalizeDecimal("Product.SalesDiscount1", value => Product.SalesDiscount1 = value);
        NormalizeDecimal("Product.SalesDiscount2", value => Product.SalesDiscount2 = value);
        NormalizeDecimal("Product.SalesDiscount3", value => Product.SalesDiscount3 = value);
        NormalizeDecimal("Product.SalesDiscount4", value => Product.SalesDiscount4 = value);
        NormalizeDecimal("Product.Quantity", value => Product.Quantity = value);
        NormalizeDecimal("Product.CriticalStockLevel", value => Product.CriticalStockLevel = value);
    }

    private void NormalizeDecimal(string key, Action<decimal> assign)
    {
        var raw = Request.Form[key].ToString();
        if (string.IsNullOrWhiteSpace(raw) || !TryParsePostedDecimal(raw, out var value)) return;

        ModelState.Remove(key);
        assign(value);
    }

    private void NormalizeInteger(string key, Action<int> assign)
    {
        var raw = Request.Form[key].ToString();
        if (string.IsNullOrWhiteSpace(raw)) return;
        if (TryParsePostedDecimal(raw, out var number))
        {
            ModelState.Remove(key);
            assign((int)Math.Round(number, MidpointRounding.AwayFromZero));
        }
    }

    private static bool TryParsePostedDecimal(string raw, out decimal value)
    {
        if (raw.Contains(",", StringComparison.Ordinal) && !raw.Contains(".", StringComparison.Ordinal) &&
            decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out value))
        {
            return true;
        }

        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
