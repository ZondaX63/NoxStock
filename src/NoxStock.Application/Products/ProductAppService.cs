using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using NoxStock.Brands;
using NoxStock.Categories;
using NoxStock.BusinessPartners;
using NoxStock.Documents;
using NoxStock.MultiTenancy;
using NoxStock.Permissions;
using Volo.Abp;

namespace NoxStock.Products;

[Authorize(NoxStockPermissions.Products.Default)]
public class ProductAppService :
    CrudAppService<
        Product,
        ProductDto,
        Guid,
        ProductGetListInput,
        CreateUpdateProductDto>,
    IProductAppService
{
    private readonly IRepository<Brand, Guid> _brandRepository;
    private readonly IRepository<Category, Guid> _categoryRepository;
    private readonly IRepository<ProductOEMCode, Guid> _productOemCodeRepository;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IRepository<ProductCostHistory, Guid> _costHistoryRepository;
    private readonly IProductPriceCalculator _priceCalculator;
    private readonly NoxStock.Licensing.ITenantLicenseAccessChecker _licenseAccessChecker;

    public ProductAppService(
        IRepository<Product, Guid> repository,
        IRepository<Brand, Guid> brandRepository,
        IRepository<Category, Guid> categoryRepository,
        IRepository<ProductOEMCode, Guid> productOemCodeRepository,
        IRepository<Document, Guid> documentRepository,
        IRepository<BusinessPartner, Guid> businessPartnerRepository,
        IRepository<ProductCostHistory, Guid> costHistoryRepository,
        IProductPriceCalculator priceCalculator,
        NoxStock.Licensing.ITenantLicenseAccessChecker licenseAccessChecker)
        : base(repository)
    {
        _brandRepository = brandRepository;
        _categoryRepository = categoryRepository;
        _productOemCodeRepository = productOemCodeRepository;
        _documentRepository = documentRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _costHistoryRepository = costHistoryRepository;
        _priceCalculator = priceCalculator;
        _licenseAccessChecker = licenseAccessChecker;

        GetPolicyName = NoxStockPermissions.Products.Default;
        GetListPolicyName = NoxStockPermissions.Products.Default;
        CreatePolicyName = NoxStockPermissions.Products.Create;
        UpdatePolicyName = NoxStockPermissions.Products.Edit;
        DeletePolicyName = NoxStockPermissions.Products.Delete;
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    public override async Task<ProductDto> CreateAsync(CreateUpdateProductDto input)
    {
        CurrentTenant.RequireTenantId();
        NormalizePricingConfiguration(input);
        ValidateFinancialConfiguration(input);
        await EnsurePricingEntitlementsAsync(input);
        await _licenseAccessChecker.EnsureLimitAsync(
            NoxStock.Licensing.LicenseLimitNames.MaxProducts,
            await Repository.GetCountAsync());
        await ValidateRelationsAsync(input);
        var result = await base.CreateAsync(input);
        await ReplaceOemCodesAsync(result.Id, input.OemCodes);
        result.OemCodes = NormalizeOemCodes(input.OemCodes);
        return result;
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    public override async Task<ProductDto> UpdateAsync(Guid id, CreateUpdateProductDto input)
    {
        CurrentTenant.RequireTenantId();
        NormalizePricingConfiguration(input);
        ValidateFinancialConfiguration(input);
        await EnsurePricingEntitlementsAsync(input);
        var existingProduct = await Repository.GetAsync(id);
        if (existingProduct.LastPurchaseCostHistoryId.HasValue &&
            !string.Equals(existingProduct.PurchaseCurrency, input.PurchaseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("NoxStock:PricingCurrencyLockedByApprovedCost")
                .WithData("Currency", existingProduct.PurchaseCurrency);
        }
        await ValidateRelationsAsync(input);
        var result = await base.UpdateAsync(id, input);
        await ReplaceOemCodesAsync(id, input.OemCodes);
        result.OemCodes = NormalizeOemCodes(input.OemCodes);
        return result;
    }

    public override async Task<ProductDto> GetAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        var queryable = await Repository.GetQueryableAsync();
        
        var query = from product in queryable
                    join brand in await _brandRepository.GetQueryableAsync() on product.BrandId equals brand.Id into brands
                    from brand in brands.DefaultIfEmpty()
                    join category in await _categoryRepository.GetQueryableAsync() on product.CategoryId equals category.Id into categories
                    from category in categories.DefaultIfEmpty()
                    where product.Id == id
                    select new { product, brand, category };

        var result = await AsyncExecuter.FirstOrDefaultAsync(query);
        
        if (result == null)
        {
            throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Product), id);
        }

        var productDto = ObjectMapper.Map<Product, ProductDto>(result.product);
        productDto.BrandName = result.brand?.Name ?? string.Empty;
        productDto.CategoryName = result.category?.Name ?? string.Empty;
        productDto.OemCodes = await GetOemCodesAsync(id);
        await PopulateLastPurchaseContextAsync(productDto);
        await PopulateSuggestedSaleAsync(result.product, productDto);

        return productDto;
    }

    public async Task<PagedResultDto<ProductLookupDto>> GetLookupListAsync(ProductLookupInput input)
    {
        CurrentTenant.RequireTenantId();

        var query = await Repository.GetQueryableAsync();
        var filter = input.Filter?.Trim();
        var normalizedFilter = filter?.ToLowerInvariant();
        var normalizedOemFilter = ProductOemCodeNormalizer.Normalize(filter);
        var ids = input.Ids?.Distinct().ToList();

        query = query.WhereIf(ids is { Count: > 0 }, product => ids!.Contains(product.Id));

        long? exactCount = null;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var exactQuery = query.Where(product =>
                product.Sku == filter ||
                product.Barcode == filter ||
                (normalizedOemFilter.Length > 0 &&
                 product.OemCodes.Any(code => code.SearchCode == normalizedOemFilter)));
            exactCount = await AsyncExecuter.CountAsync(exactQuery);
            query = exactCount > 0
                ? exactQuery
                : query.Where(product =>
                product.Name.ToLower().Contains(normalizedFilter!) ||
                product.Sku.ToLower().Contains(normalizedFilter!) ||
                (product.Barcode != null && product.Barcode.ToLower().Contains(normalizedFilter!)) ||
                (normalizedOemFilter.Length > 0 &&
                 product.OemCodes.Any(code => code.SearchCode.Contains(normalizedOemFilter))));
        }

        var totalCount = exactCount is > 0
            ? exactCount.Value
            : await AsyncExecuter.CountAsync(query);
        var maxResultCount = Math.Clamp(input.MaxResultCount, 1, 50);
        var skipCount = Math.Max(input.SkipCount, 0);
        var rows = await AsyncExecuter.ToListAsync(query
            .OrderBy(product => product.Name)
            .ThenBy(product => product.Sku)
            .Skip(skipCount)
            .Take(maxResultCount)
            .Select(product => new
            {
                product.Id,
                product.Name,
                product.Sku,
                product.Barcode,
                product.Unit,
                product.Quantity
            }));

        return new PagedResultDto<ProductLookupDto>(
            totalCount,
            rows.Select(row => new ProductLookupDto
            {
                Id = row.Id,
                Name = row.Name,
                Sku = row.Sku,
                Barcode = row.Barcode,
                Unit = row.Unit,
                Quantity = row.Quantity
            }).ToList());
    }

    protected override async Task<IQueryable<Product>> CreateFilteredQueryAsync(ProductGetListInput input)
    {
        var queryable = await Repository.GetQueryableAsync();
        var filter = input.Filter?.Trim();
        var normalizedOemFilter = ProductOemCodeNormalizer.Normalize(filter);
        var stockStatus = input.StockStatus?.Trim().ToLowerInvariant();
        var pricingMode = input.PricingMode?.Trim().ToLowerInvariant();
        var costStatus = input.CostStatus?.Trim().ToLowerInvariant();
        var recentCostThreshold = Clock.Now.AddDays(-30);

        queryable = queryable
            .WhereIf(input.BrandId.HasValue, x => x.BrandId == input.BrandId)
            .WhereIf(input.CategoryId.HasValue, x => x.CategoryId == input.CategoryId)
            .WhereIf(stockStatus == "critical", x => x.TrackStock && x.Quantity > 0 && x.Quantity <= x.CriticalStockLevel)
            .WhereIf(stockStatus == "zero", x => x.Quantity <= 0)
            .WhereIf(stockStatus == "attention", x => x.Quantity <= 0 || (x.TrackStock && x.Quantity <= x.CriticalStockLevel))
            .WhereIf(stockStatus == "normal", x => !x.TrackStock || x.Quantity > x.CriticalStockLevel)
            .WhereIf(pricingMode == "dynamic", x => x.PricingMode == ProductPricingMode.DynamicCurrencyMarkup)
            .WhereIf(pricingMode == "manual", x => x.PricingMode == ProductPricingMode.Manual)
            .WhereIf(costStatus == "missing", x => !x.LastPurchaseCostCurrencyAmount.HasValue || x.LastPurchaseCostCurrencyAmount <= 0)
            .WhereIf(costStatus == "available", x => x.LastPurchaseCostCurrencyAmount.HasValue && x.LastPurchaseCostCurrencyAmount > 0)
            .WhereIf(costStatus == "under-cost", x => x.PricingMode == ProductPricingMode.Manual && x.LastPurchaseCostTryAmount.HasValue && x.LastPurchaseCostTryAmount > 0 && x.SalePrice < x.LastPurchaseCostTryAmount)
            .WhereIf(costStatus == "recent", x => x.LastPurchaseDate.HasValue && x.LastPurchaseDate >= recentCostThreshold);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var exactQuery = queryable.Where(x =>
                x.Sku == filter ||
                x.Barcode == filter ||
                (normalizedOemFilter.Length > 0 && x.OemCodes.Any(o => o.SearchCode == normalizedOemFilter)));
            queryable = await AsyncExecuter.CountAsync(exactQuery) > 0
                ? exactQuery
                : queryable.Where(x =>
                    x.Name.Contains(filter!) ||
                    x.Sku.Contains(filter!) ||
                    (x.Barcode != null && x.Barcode.Contains(filter!)) ||
                    (normalizedOemFilter.Length > 0 && x.OemCodes.Any(o => o.SearchCode.Contains(normalizedOemFilter))));
        }

        return queryable;
    }

    public override async Task<PagedResultDto<ProductDto>> GetListAsync(ProductGetListInput input)
    {
        CurrentTenant.RequireTenantId();
        input.SkipCount = Math.Clamp(input.SkipCount, 0, 10_000);
        input.MaxResultCount = Math.Clamp(input.MaxResultCount, 1, 100);
        var queryable = await CreateFilteredQueryAsync(input);

        var totalCount = await AsyncExecuter.CountAsync(queryable);

        queryable = ApplySorting(queryable, input);
        queryable = ApplyPaging(queryable, input);

        var query = from product in queryable
                    join brand in await _brandRepository.GetQueryableAsync() on product.BrandId equals brand.Id into brands
                    from brand in brands.DefaultIfEmpty()
                    join category in await _categoryRepository.GetQueryableAsync() on product.CategoryId equals category.Id into categories
                    from category in categories.DefaultIfEmpty()
                    join history in await _costHistoryRepository.GetQueryableAsync()
                        on product.LastPurchaseCostHistoryId equals (Guid?)history.Id into histories
                    from history in histories.DefaultIfEmpty()
                    join document in await _documentRepository.GetQueryableAsync()
                        on product.LastPurchaseDocumentId equals (Guid?)document.Id into documents
                    from document in documents.DefaultIfEmpty()
                    join supplier in await _businessPartnerRepository.GetQueryableAsync()
                        on product.LastPurchaseSupplierId equals (Guid?)supplier.Id into suppliers
                    from supplier in suppliers.DefaultIfEmpty()
                    select new
                    {
                        product,
                        brand,
                        category,
                        LastPurchaseSourceCurrencyCode = history != null ? history.SourceCurrencyCode : null,
                        LastPurchaseSourceNetUnitCost = history != null ? (decimal?)history.SourceNetUnitCost : null,
                        LastPurchaseSourceExchangeRate = history != null ? (decimal?)history.SourceExchangeRate : null,
                        LastPurchaseDocumentNumber = document != null ? document.DocNumber : null,
                        LastPurchaseSupplierName = supplier != null ? supplier.Name : null
                    };

        var queryResult = await AsyncExecuter.ToListAsync(query);
        var productIds = queryResult.Select(x => x.product.Id).ToList();
        var oemCodesByProductId = productIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await AsyncExecuter.ToListAsync((await _productOemCodeRepository.GetQueryableAsync())
                    .Where(code => productIds.Contains(code.ProductId))))
                .GroupBy(code => code.ProductId)
                .ToDictionary(
                    group => group.Key,
                    group => string.Join(", ", group.Select(code => code.OemCode)));

        var productDtos = new List<ProductDto>();
        foreach (var x in queryResult)
        {
            var dto = ObjectMapper.Map<Product, ProductDto>(x.product);
            dto.BrandName = x.brand?.Name ?? string.Empty;
            dto.CategoryName = x.category?.Name ?? string.Empty;
            dto.OemCodes = oemCodesByProductId.GetValueOrDefault(x.product.Id);
            dto.LastPurchaseSourceCurrencyCode = x.LastPurchaseSourceCurrencyCode;
            dto.LastPurchaseSourceNetUnitCost = x.LastPurchaseSourceNetUnitCost;
            dto.LastPurchaseSourceExchangeRate = x.LastPurchaseSourceExchangeRate;
            dto.LastPurchaseDocumentNumber = x.LastPurchaseDocumentNumber;
            dto.LastPurchaseSupplierName = x.LastPurchaseSupplierName;
            await PopulateSuggestedSaleAsync(x.product, dto);
            productDtos.Add(dto);
        }

        return new PagedResultDto<ProductDto>(totalCount, productDtos);
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    public override Task DeleteAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        return base.DeleteAsync(id);
    }

    private async Task ReplaceOemCodesAsync(Guid productId, string? oemCodes)
    {
        var queryable = await _productOemCodeRepository.GetQueryableAsync();
        var existingCodes = await AsyncExecuter.ToListAsync(queryable.Where(x => x.ProductId == productId));

        foreach (var existingCode in existingCodes)
        {
            await _productOemCodeRepository.DeleteAsync(existingCode);
        }

        foreach (var oemCode in SplitOemCodes(oemCodes))
        {
            await _productOemCodeRepository.InsertAsync(
                new ProductOEMCode(GuidGenerator.Create(), CurrentTenant.Id, productId, oemCode));
        }
    }

    private async Task ValidateRelationsAsync(CreateUpdateProductDto input)
    {
        if (input.BrandId.HasValue && await _brandRepository.FindAsync(input.BrandId.Value) is null)
        {
            throw new BusinessException("NoxStock:InvalidBrandReference");
        }

        if (input.CategoryId.HasValue && await _categoryRepository.FindAsync(input.CategoryId.Value) is null)
        {
            throw new BusinessException("NoxStock:InvalidCategoryReference");
        }
    }

    private static void NormalizePricingConfiguration(CreateUpdateProductDto input)
    {
        input.PurchaseCurrency = NormalizeCurrency(input.PurchaseCurrency);

        if (input.UseCurrentExchangeRateForSale)
        {
            input.PricingMode = ProductPricingMode.DynamicCurrencyMarkup;
        }

        if (input.ProfitMarkupPercent < 0)
        {
            throw new BusinessException("NoxStock:InvalidProfitMarkupPercent");
        }

        if (input.PurchaseListPrice <= 0 && input.PurchasePrice > 0)
        {
            input.PurchaseListPrice = input.PurchasePrice;
        }
        if (input.ExchangeRateAtPurchase <= 0 && input.PurchaseCurrency == "TRY")
        {
            input.ExchangeRateAtPurchase = 1m;
        }
        input.UseCurrentExchangeRateForSale = input.PricingMode == ProductPricingMode.DynamicCurrencyMarkup;
        if (input.PricingMode == ProductPricingMode.DynamicCurrencyMarkup)
        {
            // Dynamic cost is sourced exclusively from the latest approved purchase line.
            input.PurchasePrice = 0m;
            input.SalePrice = 0m;
        }
    }

    private static void ValidateFinancialConfiguration(CreateUpdateProductDto input)
    {
        var percentages = new[]
        {
            input.PurchaseDiscount1, input.PurchaseDiscount2,
            input.PurchaseDiscount3, input.PurchaseDiscount4,
            input.SalesDiscount1, input.SalesDiscount2,
            input.SalesDiscount3, input.SalesDiscount4
        };
        if (percentages.Any(value => value is < 0m or > 100m))
        {
            throw new BusinessException("NoxStock:ProductDiscountOutOfRange");
        }

        if (input.VatRate is not (0 or 1 or 10 or 20))
        {
            throw new BusinessException("NoxStock:UnsupportedVatRate")
                .WithData("VatRate", input.VatRate);
        }

        if (input.PurchaseListPrice < 0m || input.PurchasePrice < 0m ||
            input.SalePrice < 0m || input.ExchangeRateAtPurchase <= 0m)
        {
            throw new BusinessException("NoxStock:InvalidProductPricingValue");
        }
    }

    private static string NormalizeCurrency(string? currency)
    {
        var normalized = currency?.Trim().ToUpperInvariant();
        if (normalized is not ("TRY" or "USD" or "EUR"))
        {
            throw new BusinessException("NoxStock:UnsupportedCurrency").WithData("Currency", currency);
        }

        return normalized;
    }

    private async Task EnsurePricingEntitlementsAsync(CreateUpdateProductDto input)
    {
        if (input.PricingMode == ProductPricingMode.DynamicCurrencyMarkup)
        {
            await _licenseAccessChecker.EnsureFeatureAsync(
                NoxStock.Licensing.LicenseFeatureNames.DynamicPricing);
        }

        if (input.PurchaseCurrency != "TRY")
        {
            await _licenseAccessChecker.EnsureFeatureAsync(
                NoxStock.Licensing.LicenseFeatureNames.MultiCurrency);
        }
    }

    private async Task<string?> GetOemCodesAsync(Guid productId)
    {
        var queryable = await _productOemCodeRepository.GetQueryableAsync();
        var codes = await AsyncExecuter.ToListAsync(queryable.Where(x => x.ProductId == productId));
        return string.Join(", ", codes.Select(x => x.OemCode));
    }

    private async Task PopulateLastPurchaseContextAsync(ProductDto product)
    {
        if (product.LastPurchaseCostHistoryId.HasValue)
        {
            var history = await _costHistoryRepository.FindAsync(product.LastPurchaseCostHistoryId.Value);
            if (history != null)
            {
                product.LastPurchaseSourceCurrencyCode = history.SourceCurrencyCode;
                product.LastPurchaseSourceNetUnitCost = history.SourceNetUnitCost;
                product.LastPurchaseSourceExchangeRate = history.SourceExchangeRate;
            }
        }

        if (product.LastPurchaseDocumentId.HasValue)
        {
            product.LastPurchaseDocumentNumber = (await _documentRepository.FindAsync(product.LastPurchaseDocumentId.Value))?.DocNumber ?? string.Empty;
        }

        if (product.LastPurchaseSupplierId.HasValue)
        {
            product.LastPurchaseSupplierName = (await _businessPartnerRepository.FindAsync(product.LastPurchaseSupplierId.Value))?.Name ?? string.Empty;
        }
    }

    private async Task PopulateSuggestedSaleAsync(Product product, ProductDto dto)
    {
        try
        {
            var calculation = await _priceCalculator.CalculateAsync(product);
            dto.IsSuggestedSaleAvailable = calculation.IsAvailable;
            dto.SuggestedSaleUnavailableReason = calculation.UnavailableReason;
            dto.SuggestedSaleUnitPrice = calculation.IsAvailable ? calculation.UnitPrice : null;
            dto.SuggestedSalePriceInTry = calculation.IsAvailable ? calculation.PriceInTry : null;
            dto.SuggestedSaleExchangeRate = calculation.ExchangeRate;
            dto.SuggestedSaleRateDate = calculation.RateDate;
            dto.CurrentReferenceCurrencyExchangeRate = calculation.ReferenceCurrencyExchangeRate;
            dto.CurrentReferenceCurrencyRateDate = calculation.ReferenceCurrencyRateDate;
            dto.CurrentNetCostInTry = calculation.NetCostInTry;
            dto.ReferenceSaleUnitPrice = calculation.ReferenceSaleUnitPrice;
        }
        catch (BusinessException exception) when (exception.Code == "NoxStock:CurrentExchangeRateUnavailable")
        {
            dto.IsSuggestedSaleAvailable = false;
            dto.SuggestedSaleUnavailableReason = exception.Code;
        }
    }

    private static string? NormalizeOemCodes(string? oemCodes)
    {
        var codes = SplitOemCodes(oemCodes).ToList();
        return codes.Count == 0 ? null : string.Join(", ", codes);
    }

    private static IEnumerable<string> SplitOemCodes(string? oemCodes)
    {
        return (oemCodes ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(code => ProductOemCodeNormalizer.Normalize(code).Length > 0)
            .GroupBy(ProductOemCodeNormalizer.Normalize, StringComparer.Ordinal)
            .Select(group => group.First());
    }
}
