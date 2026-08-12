using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.Permissions;
using NoxStock.MultiTenancy;
using NoxStock.Products;
using NoxStock.Documents;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace NoxStock.StockMovements;

[Authorize(NoxStockPermissions.StockMovements.Default)]
public class StockMovementAppService : ReadOnlyAppService<StockMovement, StockMovementDto, Guid, StockMovementGetListInput>, IStockMovementAppService
{
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<DocumentItem, Guid> _documentItemRepository;

    public StockMovementAppService(
        IRepository<StockMovement, Guid> repository,
        IRepository<Product, Guid> productRepository,
        IRepository<Document, Guid> documentRepository,
        IRepository<DocumentItem, Guid> documentItemRepository)
        : base(repository)
    {
        _productRepository = productRepository;
        _documentRepository = documentRepository;
        _documentItemRepository = documentItemRepository;
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
        input.SkipCount = Math.Clamp(input.SkipCount, 0, 10_000);
        input.MaxResultCount = Math.Clamp(input.MaxResultCount, 1, 100);
        var query = await CreateFilteredQueryAsync(input);

        var totalCount = await AsyncExecuter.CountAsync(query);

        query = ApplySorting(query, input);
        query = ApplyPaging(query, input);

        var movements = await AsyncExecuter.ToListAsync(query);

        var productIds = movements.Select(x => x.ProductId).Distinct().ToList();
        var products = await _productRepository.GetListAsync(x => productIds.Contains(x.Id));
        var productDictionary = products.ToDictionary(x => x.Id, x => x.Name);
        var documentIds = movements.Where(x => x.DocumentId.HasValue).Select(x => x.DocumentId!.Value).Distinct().ToList();
        List<Document> documents = documentIds.Count == 0
            ? []
            : await _documentRepository.GetListAsync(x => documentIds.Contains(x.Id));
        var documentDictionary = documents.ToDictionary(x => x.Id);
        List<DocumentItem> documentItems = documentIds.Count == 0
            ? []
            : await _documentItemRepository.GetListAsync(x => documentIds.Contains(x.DocumentId) && productIds.Contains(x.ProductId));
        var documentItemDictionary = documentItems
            .GroupBy(x => (x.DocumentId, x.ProductId))
            .ToDictionary(group => group.Key, group => group.OrderByDescending(x => x.CreationTime).First());

        var dtos = new List<StockMovementDto>();
        foreach (var movement in movements)
        {
            var dto = ObjectMapper.Map<StockMovement, StockMovementDto>(movement);
            if (productDictionary.TryGetValue(movement.ProductId, out var productName))
            {
                dto.ProductName = productName;
            }
            if (movement.DocumentId.HasValue && documentDictionary.TryGetValue(movement.DocumentId.Value, out var document))
            {
                dto.DocumentNumber = document.DocNumber;
                if (movement.Type == StockMovementType.PurchaseInvoice &&
                    documentItemDictionary.TryGetValue((movement.DocumentId.Value, movement.ProductId), out var item))
                {
                    dto.PurchaseCurrency = document.Currency;
                    dto.PurchaseUnitCost = item.CostCurrencyAmount ?? item.UnitPrice;
                    dto.PurchaseExchangeRate = item.PriceExchangeRate ?? document.ExchangeRate;
                    dto.PurchaseUnitCostTry = item.CostTryAmount;
                    dto.PurchaseTotalCostTry = item.TotalCostTryAmount;
                }
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
