using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MiniExcelLibs;
using MiniExcelLibs.Attributes;
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
using NoxStock.Common;
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
        NormalizeBasicConfiguration(input);
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
        NormalizeBasicConfiguration(input);
        NormalizePricingConfiguration(input);
        ValidateFinancialConfiguration(input);
        await EnsurePricingEntitlementsAsync(input);
        var existingProduct = await Repository.GetAsync(id);
        var referenceCurrencyChanged = !string.Equals(
            existingProduct.PurchaseCurrency,
            input.PurchaseCurrency,
            StringComparison.OrdinalIgnoreCase);
        await ValidateRelationsAsync(input);
        await base.UpdateAsync(id, input);
        var savedProduct = await Repository.GetAsync(id);
        savedProduct.IsIncompleteProfile = false;
        await Repository.UpdateAsync(savedProduct, autoSave: true);

        // Approved purchase records are immutable.  The product profile is a
        // derived, current pricing view, though, so it can safely be rebased
        // to another reference currency using the original document's locked
        // exchange-rate snapshot.
        if (referenceCurrencyChanged)
        {
            var updatedProduct = await Repository.GetAsync(id);
            await RebaseCurrentCostProfileAsync(updatedProduct, input.PurchaseCurrency);
            await Repository.UpdateAsync(updatedProduct, autoSave: true);
        }

        await ReplaceOemCodesAsync(id, input.OemCodes);
        return await GetAsync(id);
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
        var normalizedFilter = SearchText.Normalize(filter);
        var normalizedOemFilter = ProductOemCodeNormalizer.Normalize(filter);
        var ids = input.Ids?.Distinct().ToList();

        query = query.WhereIf(ids is { Count: > 0 }, product => ids!.Contains(product.Id));

        long? exactCount = null;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var exactQuery = query.Where(product =>
                product.Sku.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c") == normalizedFilter ||
                (product.Barcode != null && product.Barcode.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c") == normalizedFilter) ||
                (normalizedOemFilter.Length > 0 &&
                 product.OemCodes.Any(code => code.SearchCode == normalizedOemFilter)));
            exactCount = await AsyncExecuter.CountAsync(exactQuery);
            query = exactCount > 0
                ? exactQuery
                : query.Where(product =>
                product.Name.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(normalizedFilter) ||
                product.Sku.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(normalizedFilter) ||
                (product.Barcode != null && product.Barcode.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(normalizedFilter)) ||
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
                product.Quantity,
                product.TrackStock
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
                Quantity = row.Quantity,
                TrackStock = row.TrackStock
            }).ToList());
    }

    public async Task<ProductInventorySummaryDto> GetInventorySummaryAsync()
    {
        CurrentTenant.RequireTenantId();
        var query = await Repository.GetQueryableAsync();

        return new ProductInventorySummaryDto
        {
            TotalCount = await AsyncExecuter.CountAsync(query),
            DynamicPricingCount = await AsyncExecuter.CountAsync(query.Where(x => x.PricingMode == ProductPricingMode.DynamicCurrencyMarkup)),
            MissingCostCount = await AsyncExecuter.CountAsync(query.Where(x => !x.LastPurchaseCostCurrencyAmount.HasValue || x.LastPurchaseCostCurrencyAmount <= 0)),
            AttentionStockCount = await AsyncExecuter.CountAsync(query.Where(x => x.TrackStock && x.Quantity <= x.CriticalStockLevel))
        };
    }

    protected override async Task<IQueryable<Product>> CreateFilteredQueryAsync(ProductGetListInput input)
    {
        var queryable = await Repository.GetQueryableAsync();
        var filter = input.Filter?.Trim();
        var normalizedOemFilter = ProductOemCodeNormalizer.Normalize(filter);
        var normalizedFilter = SearchText.Normalize(filter);
        var stockStatus = input.StockStatus?.Trim().ToLowerInvariant();
        var pricingMode = input.PricingMode?.Trim().ToLowerInvariant();
        var costStatus = input.CostStatus?.Trim().ToLowerInvariant();
        var recentCostThreshold = Clock.Now.AddDays(-30);

        queryable = queryable
            .WhereIf(input.BrandId.HasValue, x => x.BrandId == input.BrandId)
            .WhereIf(input.CategoryId.HasValue, x => x.CategoryId == input.CategoryId)
            .WhereIf(stockStatus == "critical", x => x.TrackStock && x.Quantity > 0 && x.Quantity <= x.CriticalStockLevel)
            .WhereIf(stockStatus == "zero", x => x.TrackStock && x.Quantity <= 0)
            .WhereIf(stockStatus == "attention", x => x.TrackStock && x.Quantity <= x.CriticalStockLevel)
            .WhereIf(stockStatus == "normal", x => x.TrackStock && x.Quantity > x.CriticalStockLevel)
            .WhereIf(stockStatus == "untracked", x => !x.TrackStock)
            .WhereIf(pricingMode == "dynamic", x => x.PricingMode == ProductPricingMode.DynamicCurrencyMarkup)
            .WhereIf(pricingMode == "manual", x => x.PricingMode == ProductPricingMode.Manual)
            .WhereIf(costStatus == "missing", x => !x.LastPurchaseCostCurrencyAmount.HasValue || x.LastPurchaseCostCurrencyAmount <= 0)
            .WhereIf(costStatus == "available", x => x.LastPurchaseCostCurrencyAmount.HasValue && x.LastPurchaseCostCurrencyAmount > 0)
            .WhereIf(costStatus == "under-cost", x => x.PricingMode == ProductPricingMode.Manual && x.LastPurchaseCostTryAmount.HasValue && x.LastPurchaseCostTryAmount > 0 && x.SalePrice < x.LastPurchaseCostTryAmount)
            .WhereIf(costStatus == "recent", x => x.LastPurchaseDate.HasValue && x.LastPurchaseDate >= recentCostThreshold);
        queryable = queryable.WhereIf(costStatus == "incomplete", x => x.IsIncompleteProfile);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var exactQuery = queryable.Where(x =>
                x.Sku.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c") == normalizedFilter ||
                (x.Barcode != null && x.Barcode.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c") == normalizedFilter) ||
                (normalizedOemFilter.Length > 0 && x.OemCodes.Any(o => o.SearchCode == normalizedOemFilter)));
            queryable = await AsyncExecuter.CountAsync(exactQuery) > 0
                ? exactQuery
                : queryable.Where(x =>
                    x.Name.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(normalizedFilter) ||
                    x.Sku.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(normalizedFilter) ||
                    (x.Barcode != null && x.Barcode.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(normalizedFilter)) ||
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

    public async Task<ProductExportFileDto> ExportAsync()
    {
        CurrentTenant.RequireTenantId();
        var products = (await Repository.GetListAsync()).OrderBy(x => x.Name).ToList();
        var productIds = products.Select(x => x.Id).ToList();
        var brands = await _brandRepository.GetListAsync();
        var categories = await _categoryRepository.GetListAsync();
        var brandNames = brands.ToDictionary(x => x.Id, x => x.Name);
        var categoryNames = categories.ToDictionary(x => x.Id, x => x.Name);
        var oemCodes = productIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _productOemCodeRepository.GetListAsync(x => productIds.Contains(x.ProductId)))
                .GroupBy(x => x.ProductId)
                .ToDictionary(x => x.Key, x => string.Join(", ", x.Select(code => code.OemCode)));

        var rows = products.Select(product => new Dictionary<string, object?>
        {
            ["Ürün Adı"] = product.Name,
            ["SKU"] = product.Sku,
            ["Barkod"] = product.Barcode,
            ["Marka"] = product.BrandId.HasValue && brandNames.TryGetValue(product.BrandId.Value, out var brandName) ? brandName : string.Empty,
            ["Kategori"] = product.CategoryId.HasValue && categoryNames.TryGetValue(product.CategoryId.Value, out var categoryName) ? categoryName : string.Empty,
            ["Raf"] = product.ShelfLocation,
            ["Miktar"] = product.Quantity,
            ["Birim"] = product.Unit,
            ["Açıklama"] = product.Description,
            ["Etiketler"] = oemCodes.GetValueOrDefault(product.Id, string.Empty),
            ["Kritik Stok Miktarı"] = product.CriticalStockLevel
        }).ToList();

        await using var stream = new MemoryStream();
        await MiniExcel.SaveAsAsync(stream, rows);
        return new ProductExportFileDto
        {
            FileName = $"noxstock-urunler-{Clock.Now:yyyyMMdd-HHmm}.xlsx",
            Content = stream.ToArray()
        };
    }

    [Authorize(NoxStockPermissions.Products.Create)]
    [Authorize(NoxStockPermissions.Products.Edit)]
    [NoxStock.Licensing.TenantLicenseWrite]
    public async Task<ProductImportResultDto> ImportAsync(ProductImportInputDto input)
    {
        var tenantId = CurrentTenant.RequireTenantId();
        ValidateImportFile(input);

        List<ProductImportSpreadsheetRow> spreadsheetRows;
        try
        {
            using var stream = new MemoryStream(input.Content, writable: false);
            spreadsheetRows = (await MiniExcel.QueryAsync<ProductImportSpreadsheetRow>(
                    stream))
                .ToList();
        }
        catch (Exception exception) when (exception is not BusinessException)
        {
            throw new BusinessException(
                    "NoxStock:ProductImportInvalidWorkbook",
                    "Excel dosyası okunamadı. Dosyanın .xlsx biçiminde ve ilk satırının kolon başlıkları olduğundan emin olun.")
                .WithData("FileName", input.FileName);
        }

        var result = new ProductImportResultDto();
        var uniqueRows = new Dictionary<string, NormalizedImportRow>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < spreadsheetRows.Count; index++)
        {
            var source = spreadsheetRows[index];
            var rowNumber = index + 2;
            if (source.IsEmpty())
            {
                continue;
            }

            result.SourceRowCount++;
            var row = NormalizeImportRow(source, rowNumber, result);
            if (row is null)
            {
                result.SkippedRowCount++;
                continue;
            }

            if (!uniqueRows.TryAdd(row.Sku, row))
            {
                result.DuplicateRowCount++;
            }
        }

        result.UniqueProductCount = uniqueRows.Count;
        if (uniqueRows.Count == 0)
        {
            return result;
        }

        var normalizedSkus = uniqueRows.Keys.Select(value => value.ToLowerInvariant()).ToList();
        var productQuery = await Repository.GetQueryableAsync();
        var existingProducts = await AsyncExecuter.ToListAsync(productQuery
            .Where(product => normalizedSkus.Contains(product.Sku.ToLower())));
        var existingBySku = existingProducts
            .GroupBy(product => product.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var newProductCount = uniqueRows.Keys.Count(sku => !existingBySku.ContainsKey(sku));

        if (newProductCount > 0)
        {
            var currentCount = await Repository.GetCountAsync();
            await _licenseAccessChecker.EnsureLimitAsync(
                NoxStock.Licensing.LicenseLimitNames.MaxProducts,
                currentCount + newProductCount - 1);
        }

        var brands = await _brandRepository.GetListAsync();
        var brandByName = brands
            .GroupBy(brand => brand.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var categories = await _categoryRepository.GetListAsync();
        var categoryByName = categories
            .GroupBy(category => category.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var name in uniqueRows.Values
                     .Select(row => row.BrandName)
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (brandByName.ContainsKey(name!))
            {
                continue;
            }

            var brand = new Brand(GuidGenerator.Create(), tenantId, name!);
            await _brandRepository.InsertAsync(brand, autoSave: false);
            brandByName.Add(name!, brand);
            result.CreatedBrandCount++;
        }

        foreach (var name in uniqueRows.Values
                     .Select(row => row.CategoryName)
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (categoryByName.ContainsKey(name!))
            {
                continue;
            }

            var category = new Category(GuidGenerator.Create(), tenantId, name!);
            await _categoryRepository.InsertAsync(category, autoSave: false);
            categoryByName.Add(name!, category);
            result.CreatedCategoryCount++;
        }

        var importedProducts = new List<(Product Product, NormalizedImportRow Row)>();
        foreach (var row in uniqueRows.Values)
        {
            var isNew = !existingBySku.TryGetValue(row.Sku, out var product);
            if (isNew)
            {
                product = new Product(GuidGenerator.Create(), tenantId, row.Name, row.Sku)
                {
                    Unit = "Adet",
                    Barcode = string.Empty,
                    Description = string.Empty,
                    ShelfLocation = string.Empty,
                    PurchaseCurrency = "TRY",
                    ExchangeRateAtPurchase = 1m,
                    VatRate = 20,
                    ProfitMarkupPercent = 30m,
                    PricingMode = ProductPricingMode.Manual,
                    TrackStock = true
                };
            }

            ApplyImportedFields(
                product!,
                row,
                FindId(brandByName, row.BrandName),
                FindId(categoryByName, row.CategoryName),
                isNew);

            if (isNew)
            {
                await Repository.InsertAsync(product!, autoSave: false);
                existingBySku.Add(row.Sku, product!);
                result.CreatedCount++;
            }
            else
            {
                await Repository.UpdateAsync(product!, autoSave: false);
                result.UpdatedCount++;
            }

            importedProducts.Add((product!, row));
        }

        var replaceOemProductIds = importedProducts
            .Where(item => !string.IsNullOrWhiteSpace(item.Row.OemCodes))
            .Select(item => item.Product.Id)
            .ToList();
        if (replaceOemProductIds.Count > 0)
        {
            var existingOemCodes = await _productOemCodeRepository.GetListAsync(
                code => replaceOemProductIds.Contains(code.ProductId));
            if (existingOemCodes.Count > 0)
            {
                await _productOemCodeRepository.DeleteManyAsync(existingOemCodes, autoSave: false);
            }

            foreach (var item in importedProducts.Where(item => !string.IsNullOrWhiteSpace(item.Row.OemCodes)))
            {
                foreach (var oemCode in SplitOemCodes(item.Row.OemCodes))
                {
                    await _productOemCodeRepository.InsertAsync(
                        new ProductOEMCode(GuidGenerator.Create(), tenantId, item.Product.Id, oemCode),
                        autoSave: false);
                }
            }
        }

        return result;
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

    private static void NormalizeBasicConfiguration(CreateUpdateProductDto input)
    {
        input.Name = input.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new BusinessException("NoxStock:ProductNameRequired", "Ürün adı zorunludur.");
        }

        input.Sku = input.Sku?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input.Sku))
        {
            input.Sku = GenerateSku();
        }

        input.Barcode = input.Barcode?.Trim() ?? string.Empty;
        input.Description = input.Description?.Trim() ?? string.Empty;
        input.Unit = string.IsNullOrWhiteSpace(input.Unit) ? "Adet" : input.Unit.Trim();
        input.ShelfLocation = input.ShelfLocation?.Trim() ?? string.Empty;
    }

    private static string GenerateSku()
    {
        return $"STK-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }

    private static void ValidateImportFile(ProductImportInputDto input)
    {
        if (input.Content.Length == 0 || input.Content.Length > ProductImportInputDto.MaximumFileSize)
        {
            throw new BusinessException(
                "NoxStock:ProductImportInvalidFileSize",
                "Excel dosyası boş olamaz ve 10 MB sınırını aşamaz.");
        }

        if (!string.Equals(Path.GetExtension(input.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase) ||
            input.Content.Length < 4 || input.Content[0] != (byte)'P' || input.Content[1] != (byte)'K')
        {
            throw new BusinessException(
                "NoxStock:ProductImportInvalidFileType",
                "Yalnızca geçerli .xlsx dosyaları içe aktarılabilir.");
        }
    }

    private static NormalizedImportRow? NormalizeImportRow(
        ProductImportSpreadsheetRow source,
        int rowNumber,
        ProductImportResultDto result)
    {
        var name = Limit(CellText(source.Name), 150);
        if (string.IsNullOrWhiteSpace(name))
        {
            AddImportIssue(result, rowNumber, "Ürün adı boş olduğu için satır atlandı.");
            return null;
        }

        var sku = Limit(CellText(source.Sku), 100);
        if (string.IsNullOrWhiteSpace(sku))
        {
            sku = GenerateSku();
        }

        var rawBarcode = CellText(source.Barcode);
        var barcode = Limit(rawBarcode, 100);
        if (LooksLikeScientificNotation(rawBarcode))
        {
            barcode = string.Empty;
            result.IgnoredScientificBarcodeCount++;
        }

        return new NormalizedImportRow
        {
            RowNumber = rowNumber,
            Name = name,
            Sku = sku,
            Barcode = barcode,
            BrandName = Limit(CellText(source.Brand), 150),
            CategoryName = Limit(CellText(source.Category), 150),
            ShelfLocation = Limit(CellText(source.ShelfLocation), 100),
            Unit = Limit(CellText(source.Unit), 50),
            Description = CellText(source.Description),
            OemCodes = Limit(CellText(source.Tags), 1000),
            Quantity = ParseDecimal(source.Quantity),
            CriticalStockLevel = ParseDecimal(source.CriticalStockLevel)
        };
    }

    private static void ApplyImportedFields(
        Product product,
        NormalizedImportRow row,
        Guid? brandId,
        Guid? categoryId,
        bool isNew)
    {
        product.Name = row.Name;
        product.Sku = row.Sku;
        if (!string.IsNullOrWhiteSpace(row.Barcode)) product.Barcode = row.Barcode;
        if (!string.IsNullOrWhiteSpace(row.Description)) product.Description = row.Description;
        if (!string.IsNullOrWhiteSpace(row.Unit)) product.Unit = row.Unit;
        if (!string.IsNullOrWhiteSpace(row.ShelfLocation)) product.ShelfLocation = row.ShelfLocation;
        if (brandId.HasValue) product.BrandId = brandId;
        if (categoryId.HasValue) product.CategoryId = categoryId;
        if (row.Quantity.HasValue) product.Quantity = row.Quantity.Value;
        if (row.CriticalStockLevel.HasValue) product.CriticalStockLevel = row.CriticalStockLevel.Value;
        if (isNew)
        {
            product.TrackStock = true;
        }
    }

    private static Guid? FindId<T>(Dictionary<string, T> values, string? name)
        where T : Volo.Abp.Domain.Entities.IEntity<Guid>
    {
        return !string.IsNullOrWhiteSpace(name) && values.TryGetValue(name, out var value)
            ? value.Id
            : null;
    }

    private static string CellText(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text.Trim(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
            _ => value.ToString()?.Trim() ?? string.Empty
        };
    }

    private static decimal? ParseDecimal(object? value)
    {
        if (value is null) return null;
        if (value is decimal decimalValue) return decimalValue;
        if (value is IConvertible convertible && value is not string)
        {
            try
            {
                return Convert.ToDecimal(convertible, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return null;
            }
        }

        var text = CellText(value)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\u00A0", string.Empty, StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(text)) return null;

        var commaIndex = text.LastIndexOf(',');
        var dotIndex = text.LastIndexOf('.');
        if (commaIndex >= 0 && dotIndex >= 0)
        {
            // When both separators exist, the last one is the decimal mark.
            // This accepts both 1.234,50 and 1,234.50 without relying on server culture.
            var decimalSeparator = commaIndex > dotIndex ? ',' : '.';
            var groupSeparator = decimalSeparator == ',' ? "." : ",";
            var normalized = text.Replace(groupSeparator, string.Empty, StringComparison.Ordinal)
                .Replace(decimalSeparator, '.');
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var formattedValue)
                ? formattedValue
                : null;
        }

        if (dotIndex >= 0)
        {
            // Spreadsheet exports commonly write quantities as strings such as "1.0".
            // Parsing this under tr-TR would turn it into ten because '.' is a group separator.
            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var dotDecimalValue)
                ? dotDecimalValue
                : null;
        }

        if (commaIndex >= 0)
        {
            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("tr-TR"), out var commaDecimalValue)
                ? commaDecimalValue
                : null;
        }

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var integerValue)
            ? integerValue
            : null;
    }

    private static bool LooksLikeScientificNotation(string value)
    {
        return Regex.IsMatch(value, @"^[+-]?\d+(?:[\.,]\d+)?[eE][+-]?\d+$", RegexOptions.CultureInvariant);
    }

    private static string Limit(string value, int maximumLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maximumLength ? trimmed : trimmed[..maximumLength];
    }

    private static void AddImportIssue(ProductImportResultDto result, int rowNumber, string message)
    {
        if (result.Issues.Count >= 50) return;
        result.Issues.Add(new ProductImportIssueDto { RowNumber = rowNumber, Message = message });
    }

    private sealed class NormalizedImportRow
    {
        public int RowNumber { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Sku { get; init; } = string.Empty;
        public string Barcode { get; init; } = string.Empty;
        public string BrandName { get; init; } = string.Empty;
        public string CategoryName { get; init; } = string.Empty;
        public string ShelfLocation { get; init; } = string.Empty;
        public string Unit { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string OemCodes { get; init; } = string.Empty;
        public decimal? Quantity { get; init; }
        public decimal? CriticalStockLevel { get; init; }
    }

    private sealed class ProductImportSpreadsheetRow
    {
        [ExcelColumnName("Ürün Adı")] public object? Name { get; set; }
        [ExcelColumnName("SKU")] public object? Sku { get; set; }
        [ExcelColumnName("Barkod")] public object? Barcode { get; set; }
        [ExcelColumnName("Marka")] public object? Brand { get; set; }
        [ExcelColumnName("Kategori")] public object? Category { get; set; }
        [ExcelColumnName("Raf")] public object? ShelfLocation { get; set; }
        [ExcelColumnName("Miktar")] public object? Quantity { get; set; }
        [ExcelColumnName("Birim")] public object? Unit { get; set; }
        [ExcelColumnName("Açıklama")] public object? Description { get; set; }
        [ExcelColumnName("Etiketler")] public object? Tags { get; set; }
        [ExcelColumnName("Kritik Stok Miktarı")] public object? CriticalStockLevel { get; set; }

        public bool IsEmpty()
        {
            return string.IsNullOrWhiteSpace(CellText(Name)) &&
                   string.IsNullOrWhiteSpace(CellText(Sku)) &&
                   string.IsNullOrWhiteSpace(CellText(Barcode)) &&
                   string.IsNullOrWhiteSpace(CellText(Brand)) &&
                   string.IsNullOrWhiteSpace(CellText(Category)) &&
                   string.IsNullOrWhiteSpace(CellText(ShelfLocation)) &&
                   string.IsNullOrWhiteSpace(CellText(Quantity));
        }
    }

    private async Task RebaseCurrentCostProfileAsync(Product product, string referenceCurrency)
    {
        if (!product.LastPurchaseDocumentId.HasValue ||
            product.LastPurchaseCostTryAmount is not > 0m)
        {
            return;
        }

        var normalizedCurrency = NormalizeCurrency(referenceCurrency);
        var rateToTry = 1m;
        if (!string.Equals(normalizedCurrency, "TRY", StringComparison.OrdinalIgnoreCase))
        {
            var documents = await _documentRepository.WithDetailsAsync(x => x.ExchangeRateSnapshots);
            var sourceDocument = await AsyncExecuter.FirstOrDefaultAsync(
                documents.Where(x => x.Id == product.LastPurchaseDocumentId.Value));
            var rateSnapshot = sourceDocument?.ExchangeRateSnapshots
                .OrderByDescending(x => x.RateDate)
                .FirstOrDefault(x => string.Equals(
                    x.CurrencyCode,
                    normalizedCurrency,
                    StringComparison.OrdinalIgnoreCase));

            if (rateSnapshot?.RateToTry is not > 0m)
            {
                throw new BusinessException("NoxStock:DocumentExchangeRateSnapshotUnavailable")
                    .WithData("Currency", normalizedCurrency)
                    .WithData("DocumentId", product.LastPurchaseDocumentId.Value);
            }

            rateToTry = rateSnapshot.RateToTry;
        }

        product.LastPurchaseCostCurrencyCode = normalizedCurrency;
        product.LastPurchaseCostCurrencyAmount = RoundMoney(product.LastPurchaseCostTryAmount.Value / rateToTry);
        product.LastPurchaseExchangeRate = RoundMoney(rateToTry);
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 6, MidpointRounding.AwayFromZero);

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
