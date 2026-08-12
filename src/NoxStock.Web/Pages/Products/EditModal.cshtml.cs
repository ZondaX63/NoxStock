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
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Web.Pages.Products;

public class EditModalModel : NoxStockPageModel
{
    [HiddenInput]
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public CreateUpdateProductDto Product { get; set; } = new();

    public List<SelectListItem> Brands { get; set; } = new();
    public List<SelectListItem> Categories { get; set; } = new();
    public ProductDto? ProductSnapshot { get; private set; }
    public ProductPriceQuoteDto? PricingQuote { get; private set; }
    public List<ProductCostHistoryDto> CostHistory { get; private set; } = new();

    private readonly IProductAppService _productAppService;
    private readonly IBrandAppService _brandAppService;
    private readonly ICategoryAppService _categoryAppService;
    private readonly IProductPricingAppService _productPricingAppService;

    public EditModalModel(
        IProductAppService productAppService,
        IBrandAppService brandAppService,
        ICategoryAppService categoryAppService,
        IProductPricingAppService productPricingAppService)
    {
        _productAppService = productAppService;
        _brandAppService = brandAppService;
        _categoryAppService = categoryAppService;
        _productPricingAppService = productPricingAppService;
    }

    public async Task OnGetAsync()
    {
        var productDto = await _productAppService.GetAsync(Id);
        ProductSnapshot = productDto;
        Product = ObjectMapper.Map<ProductDto, CreateUpdateProductDto>(productDto);
        NormalizeTryExchangeRate();
        PricingQuote = await _productPricingAppService.GetQuoteAsync(new ProductPriceQuoteInputDto { ProductId = Id });
        CostHistory = await _productPricingAppService.GetCostHistoryAsync(Id);

        await LoadLookupsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // HTML number inputs always post decimals with a dot.  The active UI
        // culture can be tr-TR, where that value otherwise reaches the model
        // binder as an invalid decimal (or as a thousands separator).
        NormalizeDecimalProductFields();
        NormalizeIntegralProductFields();
        NormalizeTryExchangeRate();
        if (!ModelState.IsValid)
        {
            return ProductSaveValidationError(GetModelStateError());
        }

        try
        {
            await _productAppService.UpdateAsync(Id, Product);
        }
        catch (BusinessException exception)
        {
            return ProductSaveValidationError(GetProductSaveError(exception));
        }

        return NoContent();
    }

    private async Task LoadLookupsAsync()
    {
        var request = new PagedAndSortedResultRequestDto { MaxResultCount = 1000 };
        var brandLookup = await _brandAppService.GetListAsync(request);
        Brands = brandLookup.Items.Select(b => new SelectListItem(b.Name, b.Id.ToString())).ToList();
        Brands.Insert(0, new SelectListItem("Marka seçiniz", ""));

        var categoryLookup = await _categoryAppService.GetListAsync(request);
        Categories = categoryLookup.Items.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList();
        Categories.Insert(0, new SelectListItem("Kategori seçiniz", ""));
    }

    private static string GetProductSaveError(BusinessException exception) => exception.Code switch
    {
        "NoxStock:ProductNameRequired" => "Ürün adı zorunludur.",
        "NoxStock:InvalidProductPricingValue" => "İşlem kuru sıfırdan büyük olmalıdır; kur bilgisini yenileyin veya elle girin.",
        "NoxStock:ProductDiscountOutOfRange" => "İskonto oranları %0 ile %100 arasında olmalıdır.",
        "NoxStock:UnsupportedVatRate" => "Geçerli bir KDV oranı seçin.",
        "NoxStock:UnsupportedCurrency" => "Geçerli bir referans para birimi seçin.",
        "NoxStock:DocumentExchangeRateSnapshotUnavailable" => "Geçmiş alış belgesinde seçilen para birimi için kayıtlı kur bulunamadı.",
        _ => "Ürün kaydedilemedi. Lütfen alanları ve lisans limitlerini kontrol edin."
    };

    private IActionResult ProductSaveValidationError(string message) =>
        BadRequest(new { error = new { message } });

    private string GetModelStateError()
    {
        var errors = ModelState.Values
            .SelectMany(value => value.Errors)
            .Select(error => error.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToList();
        return errors.Count > 0
            ? string.Join(" ", errors)
            : "Ürün kaydedilemedi. Zorunlu alanları kontrol edin.";
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
        assign(Math.Round(value, 2, MidpointRounding.AwayFromZero));
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
        // A comma-only input is a local manual entry; values generated by a
        // number input use the invariant decimal point.
        if (raw.Contains(",", StringComparison.Ordinal) && !raw.Contains(".", StringComparison.Ordinal) &&
            decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out value))
        {
            return true;
        }

        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
