using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NexStock.Brands;

public class Brand : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    public string Name { get; set; }

    public Brand()
    {
    }

    public Brand(Guid id, Guid? tenantId, string name) : base(id)
    {
        TenantId = tenantId;
        Name = name;
    }
}
