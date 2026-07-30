using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.Permissions;
using NoxStock.Products;
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

    public async Task<List<LabelProductDto>> GetListAsync()
    {
        CurrentTenant.RequireTenantId();
        var products = await _productRepository.GetQueryableAsync();
        var productList = await AsyncExecuter.ToListAsync(products
            .OrderBy(x => x.Name)
            .Take(2000));
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
