using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace NoxStock.StockMovements;

public interface IStockMovementAppService : IReadOnlyAppService<StockMovementDto, Guid, StockMovementGetListInput>
{
}

public class StockMovementGetListInput : PagedAndSortedResultRequestDto
{
    public Guid? ProductId { get; set; }
    public Guid? DocumentId { get; set; }
}
