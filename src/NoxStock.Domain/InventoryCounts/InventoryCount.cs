using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NoxStock.InventoryCounts;

public class InventoryCount : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CountDate { get; set; }
    public InventoryCountStatus Status { get; set; }
    public string CountedByName { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public string? CompletedByName { get; set; }
    public int CountedProductCount { get; set; }
    public decimal TotalCountedQuantity { get; set; }
    public int ZeroedProductCount { get; set; }
    public int AdjustedProductCount { get; set; }
    public ICollection<InventoryCountItem> Items { get; protected set; } = new List<InventoryCountItem>();

    protected InventoryCount()
    {
    }

    public InventoryCount(
        Guid id,
        Guid tenantId,
        string referenceNumber,
        string name,
        DateTime countDate,
        string countedByName)
        : base(id)
    {
        TenantId = tenantId;
        ReferenceNumber = referenceNumber;
        Name = name;
        CountDate = countDate;
        CountedByName = countedByName;
        Status = InventoryCountStatus.Draft;
    }
}
