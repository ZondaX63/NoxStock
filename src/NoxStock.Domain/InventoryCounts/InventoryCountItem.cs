using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NoxStock.InventoryCounts;

public class InventoryCountItem : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    public Guid InventoryCountId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Unit { get; set; } = "Adet";
    public decimal SystemQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
    public string? Note { get; set; }
    public bool IsQuickCreatedProduct { get; set; }

    protected InventoryCountItem()
    {
    }

    public InventoryCountItem(
        Guid id,
        Guid tenantId,
        Guid inventoryCountId,
        Guid productId)
        : base(id)
    {
        TenantId = tenantId;
        InventoryCountId = inventoryCountId;
        ProductId = productId;
    }
}
