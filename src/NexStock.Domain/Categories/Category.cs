using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NexStock.Categories;

public class Category : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    public string Name { get; set; }
    public Guid? ParentId { get; set; }

    public Category()
    {
    }

    public Category(Guid id, Guid? tenantId, string name, Guid? parentId = null) : base(id)
    {
        TenantId = tenantId;
        Name = name;
        ParentId = parentId;
    }
}
