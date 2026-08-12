using System;
using Volo.Abp.Application.Dtos;

namespace NoxStock.BusinessPartners;

public class BusinessPartnerOpeningBalanceDto : FullAuditedEntityDto<Guid>
{
    public Guid BusinessPartnerId { get; set; }
    public BusinessPartnerOpeningDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public DateTime OpeningDate { get; set; }
    public string? Description { get; set; }
    public BusinessPartnerOpeningStatus Status { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}
