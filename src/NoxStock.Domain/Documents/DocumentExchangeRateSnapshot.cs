using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NoxStock.Documents;

public class DocumentExchangeRateSnapshot : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    public Guid DocumentId { get; set; }
    public string CurrencyCode { get; set; } = "TRY";
    public decimal RateToTry { get; set; }
    public DateTime RateDate { get; set; }

    protected DocumentExchangeRateSnapshot()
    {
    }

    public DocumentExchangeRateSnapshot(Guid id, Guid? tenantId, Guid documentId, string currencyCode, decimal rateToTry, DateTime rateDate)
        : base(id)
    {
        TenantId = tenantId;
        DocumentId = documentId;
        CurrencyCode = currencyCode;
        RateToTry = rateToTry;
        RateDate = rateDate;
    }
}
