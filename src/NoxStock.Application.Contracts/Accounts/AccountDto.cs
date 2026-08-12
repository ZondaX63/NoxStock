using System;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Accounts;

public class AccountDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public string Currency { get; set; } = "TRY";
    public decimal Balance { get; set; }
    public bool IsActive { get; set; }
    public decimal TodayIncome { get; set; }
    public decimal TodayExpense { get; set; }
    public DateTime? LastTransactionDate { get; set; }
}
