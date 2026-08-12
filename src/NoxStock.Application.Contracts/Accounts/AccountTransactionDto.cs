using System;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Accounts;

public class AccountTransactionDto : FullAuditedEntityDto<Guid>
{
    public Guid AccountId { get; set; }
    public Guid? DocumentId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public AccountTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public DateTime TransactionDate { get; set; }
    public string? Description { get; set; }
    public string? OperationKey { get; set; }
    public AccountTransactionStatus Status { get; set; }
    public Guid? RelatedAccountId { get; set; }
    public Guid? ReversalOfTransactionId { get; set; }
    public decimal? BalanceAfter { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ReferenceNumber { get; set; }
}
