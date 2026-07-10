using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace NexStock.Accounts;

public interface IAccountTransactionAppService : IReadOnlyAppService<AccountTransactionDto, Guid, AccountTransactionGetListInput>
{
}

public class AccountTransactionGetListInput : PagedAndSortedResultRequestDto
{
    public Guid? AccountId { get; set; }
    public Guid? DocumentId { get; set; }
}
