using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NoxStock.BusinessPartners;

public class BusinessPartner : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    
    public string Name { get; set; }
    public BusinessPartnerType Type { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? TaxNumber { get; set; }
    public string? TaxOffice { get; set; }
    public decimal Balance { get; set; }
    public decimal CreditLimit { get; set; }
    public string Currency { get; set; }

    public BusinessPartner()
    {
        Currency = "TRY";
    }

    public BusinessPartner(Guid id, Guid? tenantId, string name, BusinessPartnerType type, string currency = "TRY")
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Type = type;
        Currency = currency;
    }
}
