using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using NexStock.Brands;
using NexStock.Categories;
using NexStock.MultiTenancy;
using NexStock.Permissions;
using Volo.Abp;

namespace NexStock.Products;

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

    public ProductAppService(
        IRepository<Product, Guid> repository,
        IRepository<Brand, Guid> brandRepository,
        IRepository<Category, Guid> categoryRepository,
        IRepository<ProductOEMCode, Guid> productOemCodeRepository)
        : base(repository)
    {
        _brandRepository = brandRepository;
        _categoryRepository = categoryRepository;
        _productOemCodeRepository = productOemCodeRepository;

        GetPolicyName = NexStockPermissions.Products.Default;
        GetListPolicyName = NexStockPermissions.Products.Default;
        CreatePolicyName = NexStockPermissions.Products.Create;
        UpdatePolicyName = NexStockPermissions.Products.Edit;
        DeletePolicyName = NexStockPermissions.Products.Delete;
    }

    public override async Task<ProductDto> CreateAsync(CreateUpdateProductDto input)
    {
        CurrentTenant.RequireTenantId();
        await ValidateRelationsAsync(input);
        var result = await base.CreateAsync(input);
        await ReplaceOemCodesAsync(result.Id, input.OemCodes);
        result.OemCodes = NormalizeOemCodes(input.OemCodes);
        return result;
    }

    public override async Task<ProductDto> UpdateAsync(Guid id, CreateUpdateProductDto input)
    {
        CurrentTenant.RequireTenantId();
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
        productDto.BrandName = result.brand?.Name;
        productDto.CategoryName = result.category?.Name;
        productDto.OemCodes = await GetOemCodesAsync(id);

        return productDto;
    }

    protected override async Task<IQueryable<Product>> CreateFilteredQueryAsync(ProductGetListInput input)
    {
        var queryable = await Repository.GetQueryableAsync();
        var filter = input.Filter?.Trim();
        var stockStatus = input.StockStatus?.Trim().ToLowerInvariant();

        queryable = queryable
            .WhereIf(input.BrandId.HasValue, x => x.BrandId == input.BrandId)
            .WhereIf(input.CategoryId.HasValue, x => x.CategoryId == input.CategoryId)
            .WhereIf(!string.IsNullOrWhiteSpace(filter), x =>
                x.Name.Contains(filter!) ||
                x.Sku.Contains(filter!) ||
                (x.Barcode != null && x.Barcode.Contains(filter!)) ||
                x.OemCodes.Any(o => o.OemCode.Contains(filter!)))
            .WhereIf(stockStatus == "critical", x => x.TrackStock && x.Quantity > 0 && x.Quantity <= x.CriticalStockLevel)
            .WhereIf(stockStatus == "zero", x => x.Quantity <= 0)
            .WhereIf(stockStatus == "normal", x => !x.TrackStock || x.Quantity > x.CriticalStockLevel);

        return queryable;
    }

    public override async Task<PagedResultDto<ProductDto>> GetListAsync(ProductGetListInput input)
    {
        CurrentTenant.RequireTenantId();
        var queryable = await CreateFilteredQueryAsync(input);

        var totalCount = await AsyncExecuter.CountAsync(queryable);

        queryable = ApplySorting(queryable, input);
        queryable = ApplyPaging(queryable, input);

        var query = from product in queryable
                    join brand in await _brandRepository.GetQueryableAsync() on product.BrandId equals brand.Id into brands
                    from brand in brands.DefaultIfEmpty()
                    join category in await _categoryRepository.GetQueryableAsync() on product.CategoryId equals category.Id into categories
                    from category in categories.DefaultIfEmpty()
                    select new { product, brand, category };

        var queryResult = await AsyncExecuter.ToListAsync(query);

        var productDtos = new List<ProductDto>();
        foreach (var x in queryResult)
        {
            var dto = ObjectMapper.Map<Product, ProductDto>(x.product);
            dto.BrandName = x.brand?.Name;
            dto.CategoryName = x.category?.Name;
            dto.OemCodes = await GetOemCodesAsync(x.product.Id);
            productDtos.Add(dto);
        }

        return new PagedResultDto<ProductDto>(totalCount, productDtos);
    }

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
            throw new BusinessException("NexStock:InvalidBrandReference");
        }

        if (input.CategoryId.HasValue && await _categoryRepository.FindAsync(input.CategoryId.Value) is null)
        {
            throw new BusinessException("NexStock:InvalidCategoryReference");
        }
    }

    private async Task<string?> GetOemCodesAsync(Guid productId)
    {
        var queryable = await _productOemCodeRepository.GetQueryableAsync();
        var codes = await AsyncExecuter.ToListAsync(queryable.Where(x => x.ProductId == productId));
        return string.Join(", ", codes.Select(x => x.OemCode));
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
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
