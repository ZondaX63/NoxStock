using System;
using Volo.Abp.Application.Dtos;

namespace NexStock.Accounts;

public class AccountDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public string Currency { get; set; } = "TRY";
    public decimal Balance { get; set; }
    public bool IsActive { get; set; }
}

