using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace NoxStock.Licensing;

public class LicensePlan : FullAuditedAggregateRoot<Guid>
{
    public string Code { get; protected set; } = string.Empty;
    public string Name { get; protected set; } = string.Empty;
    public string? Description { get; protected set; }
    public bool IsActive { get; protected set; }
    public bool IsCustom { get; protected set; }

    protected LicensePlan()
    {
    }

    public LicensePlan(
        Guid id,
        string code,
        string name,
        string? description,
        bool isCustom = false) : base(id)
    {
        SetCode(code);
        SetName(name);
        Description = description?.Trim();
        IsCustom = isCustom;
        IsActive = true;
    }

    public void Update(string name, string? description, bool isActive)
    {
        SetName(name);
        Description = description?.Trim();
        IsActive = isActive;
    }

    private void SetCode(string code)
    {
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), 32).Trim().ToUpperInvariant();
    }

    private void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), 100).Trim();
    }
}
