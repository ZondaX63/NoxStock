using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NexStock.Permissions;
using NexStock.MultiTenancy;
using NexStock.Products;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace NexStock.StockMovements;

[Authorize(NexStockPermissions.Products.Default)]
public class StockMovementAppService : ReadOnlyAppService<StockMovement, StockMovementDto, Guid, StockMovementGetListInput>, IStockMovementAppService
{
    private readonly IRepository<Product, Guid> _productRepository;

    public StockMovementAppService(
        IRepository<StockMovement, Guid> repository,
        IRepository<Product, Guid> productRepository) 
        : base(repository)
    {
        _productRepository = productRepository;
    }

    protected override async Task<IQueryable<StockMovement>> CreateFilteredQueryAsync(StockMovementGetListInput input)
    {
        return (await base.CreateFilteredQueryAsync(input))
            .WhereIf(input.ProductId.HasValue, x => x.ProductId == input.ProductId)
            .WhereIf(input.DocumentId.HasValue, x => x.DocumentId == input.DocumentId);
    }

    public override async Task<PagedResultDto<StockMovementDto>> GetListAsync(StockMovementGetListInput input)
    {
        CurrentTenant.RequireTenantId();
        var query = await CreateFilteredQueryAsync(input);

        var totalCount = await AsyncExecuter.CountAsync(query);

        query = ApplySorting(query, input);
        query = ApplyPaging(query, input);

        var movements = await AsyncExecuter.ToListAsync(query);

        var productIds = movements.Select(x => x.ProductId).Distinct().ToList();
        var products = await _productRepository.GetListAsync(x => productIds.Contains(x.Id));
        var productDictionary = products.ToDictionary(x => x.Id, x => x.Name);

        var dtos = new List<StockMovementDto>();
        foreach (var movement in movements)
        {
            var dto = ObjectMapper.Map<StockMovement, StockMovementDto>(movement);
            if (productDictionary.TryGetValue(movement.ProductId, out var productName))
            {
                dto.ProductName = productName;
            }
            dtos.Add(dto);
        }

        return new PagedResultDto<StockMovementDto>(totalCount, dtos);
    }

    public override Task<StockMovementDto> GetAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        return base.GetAsync(id);
    }
}
