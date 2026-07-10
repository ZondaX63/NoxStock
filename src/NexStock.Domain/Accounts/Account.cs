using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NexStock.Accounts;

public class Account : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    public string Name { get; set; }
    public AccountType Type { get; set; }
    public string Currency { get; set; }
    public decimal Balance { get; set; }
    public bool IsActive { get; set; }

    protected Account()
    {
        Name = string.Empty;
        Currency = "TRY";
        IsActive = true;
    }

    public Account(Guid id, Guid? tenantId, string name, AccountType type, string currency = "TRY")
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Type = type;
        Currency = currency;
        IsActive = true;
    }
}

