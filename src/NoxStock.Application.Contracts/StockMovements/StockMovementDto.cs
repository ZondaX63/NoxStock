using System;
using Volo.Abp.Application.Dtos;

namespace NoxStock.StockMovements;

public class StockMovementDto : FullAuditedEntityDto<Guid>
{
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public Guid? DocumentId { get; set; }
    public StockMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTime MovementDate { get; set; }
    public string? Description { get; set; }
    public string? DocumentNumber { get; set; }
    public string? PurchaseCurrency { get; set; }
    public decimal? PurchaseUnitCost { get; set; }
    public decimal? PurchaseExchangeRate { get; set; }
    public decimal? PurchaseUnitCostTry { get; set; }
    public decimal? PurchaseTotalCostTry { get; set; }
}
