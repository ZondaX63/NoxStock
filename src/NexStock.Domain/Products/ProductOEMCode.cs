using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NexStock.Products;

public class ProductOEMCode : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    public Guid ProductId { get; set; }
    public string OemCode { get; set; }
    public string SearchCode { get; private set; } = string.Empty;

    protected ProductOEMCode()
    {
    }

    public ProductOEMCode(Guid id, Guid? tenantId, Guid productId, string oemCode) : base(id)
    {
        TenantId = tenantId;
        ProductId = productId;
        OemCode = oemCode;
        SearchCode = ProductOemCodeNormalizer.Normalize(oemCode);
    }
}
