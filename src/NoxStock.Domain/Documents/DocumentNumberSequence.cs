using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NoxStock.Documents;

public class DocumentNumberSequence : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    public DocumentType DocumentType { get; set; }
    public int Year { get; set; }
    public string Prefix { get; set; } = string.Empty;
    public int LastNumber { get; set; }
    protected DocumentNumberSequence() { }
    public DocumentNumberSequence(Guid id, Guid tenantId, DocumentType type, int year, string prefix, int lastNumber) : base(id)
    { TenantId = tenantId; DocumentType = type; Year = year; Prefix = prefix; LastNumber = lastNumber; }
}
