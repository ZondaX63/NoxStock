using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.Permissions;
using NoxStock.Products;
using NoxStock.Common;
using NoxStock.MultiTenancy;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace NoxStock.PrintLabels;

[Authorize(NoxStockPermissions.PrintLabels.Default)]
public class LabelProductLookupAppService : ApplicationService, ILabelProductLookupAppService
{
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IProductPriceCalculator _priceCalculator;

    public LabelProductLookupAppService(
        IRepository<Product, Guid> productRepository,
        IProductPriceCalculator priceCalculator)
    {
        _productRepository = productRepository;
        _priceCalculator = priceCalculator;
    }

    public async Task<List<LabelProductDto>> GetListAsync(LabelProductLookupInput input)
    {
        CurrentTenant.RequireTenantId();
        var query = await _productRepository.GetQueryableAsync();
        var filter = input.Filter?.Trim();
        var normalizedFilter = SearchText.Normalize(filter);
        var normalizedOemFilter = ProductOemCodeNormalizer.Normalize(filter);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var exactQuery = query.Where(product =>
                product.Sku.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c") == normalizedFilter ||
                (product.Barcode != null && product.Barcode.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c") == normalizedFilter) ||
                (normalizedOemFilter.Length > 0 &&
                 product.OemCodes.Any(code => code.SearchCode == normalizedOemFilter)));
            query = await AsyncExecuter.AnyAsync(exactQuery)
                ? exactQuery
                : query.Where(product =>
                    product.Name.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(normalizedFilter) ||
                    product.Sku.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(normalizedFilter) ||
                    (product.Barcode != null && product.Barcode.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(normalizedFilter)) ||
                    (normalizedOemFilter.Length > 0 &&
                     product.OemCodes.Any(code => code.SearchCode.Contains(normalizedOemFilter))));
        }

        var productList = await AsyncExecuter.ToListAsync(query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Sku)
            .Take(Math.Clamp(input.MaxResultCount, 1, 30)));
        var result = new List<LabelProductDto>();
        foreach (var product in productList)
        {
            var calculation = await _priceCalculator.CalculateAsync(product);
            result.Add(new LabelProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Sku = product.Sku,
                Barcode = product.Barcode,
                SalePrice = calculation.IsAvailable ? calculation.UnitPrice : 0m,
                Currency = "TRY"
            });
        }

        return result;
    }
}
