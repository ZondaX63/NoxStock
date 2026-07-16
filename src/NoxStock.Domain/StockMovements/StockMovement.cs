using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NoxStock.StockMovements;

public class StockMovement : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    public Guid ProductId { get; set; }
    public Guid? DocumentId { get; set; }
    public StockMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTime MovementDate { get; set; }
    public string? Description { get; set; }

    protected StockMovement()
    {
        MovementDate = DateTime.UtcNow;
    }

    public StockMovement(
        Guid id,
        Guid? tenantId,
        Guid productId,
        StockMovementType type,
        decimal quantity,
        decimal balanceAfter,
        DateTime movementDate,
        Guid? documentId = null,
        string? description = null)
        : base(id)
    {
        TenantId = tenantId;
        ProductId = productId;
        DocumentId = documentId;
        Type = type;
        Quantity = quantity;
        BalanceAfter = balanceAfter;
        MovementDate = movementDate;
        Description = description;
    }
}

