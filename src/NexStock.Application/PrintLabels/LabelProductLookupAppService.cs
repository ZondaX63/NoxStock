using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NexStock.Permissions;
using NexStock.Products;
using NexStock.MultiTenancy;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace NexStock.PrintLabels;

[Authorize(NexStockPermissions.PrintLabels.Default)]
public class LabelProductLookupAppService : ApplicationService, ILabelProductLookupAppService
{
    private readonly IRepository<Product, Guid> _productRepository;

    public LabelProductLookupAppService(IRepository<Product, Guid> productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<List<LabelProductDto>> GetListAsync()
    {
        CurrentTenant.RequireTenantId();
        var products = await _productRepository.GetQueryableAsync();
        var query = products
            .OrderBy(x => x.Name)
            .Take(2000)
            .Select(x => new LabelProductDto
            {
                Id = x.Id,
                Name = x.Name,
                Sku = x.Sku,
                Barcode = x.Barcode,
                SalePrice = x.SalePrice,
                Currency = string.IsNullOrWhiteSpace(x.SaleCurrency) ? "TRY" : x.SaleCurrency
            });

        return await AsyncExecuter.ToListAsync(query);
    }
}
