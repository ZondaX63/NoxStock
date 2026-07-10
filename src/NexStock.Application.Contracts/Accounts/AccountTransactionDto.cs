using System;
using Volo.Abp.Application.Dtos;

namespace NexStock.Accounts;

public class AccountTransactionDto : FullAuditedEntityDto<Guid>
{
    public Guid AccountId { get; set; }
    public Guid? DocumentId { get; set; }
    public AccountTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public DateTime TransactionDate { get; set; }
    public string? Description { get; set; }
}

