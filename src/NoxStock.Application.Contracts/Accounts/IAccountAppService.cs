using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace NoxStock.Accounts;

public interface IAccountAppService :
    ICrudAppService<
        AccountDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateAccountDto>
{
    System.Threading.Tasks.Task<AccountDto> CreateWithOpeningAsync(CreateAccountWithOpeningDto input);
    System.Threading.Tasks.Task<AccountTransactionDto> CreateOpeningBalanceAsync(CreateAccountOpeningBalanceDto input);
    System.Threading.Tasks.Task CancelTransactionAsync(Guid transactionId, CancelAccountTransactionDto input);
    System.Threading.Tasks.Task<PagedResultDto<AccountLookupDto>> GetLookupListAsync(AccountLookupInput input);
    System.Threading.Tasks.Task TransferAsync(TransferDto input);
    System.Threading.Tasks.Task AddTransactionAsync(AddTransactionDto input);
    System.Threading.Tasks.Task<PagedResultDto<AccountTransactionDto>> GetTransactionsAsync(Guid accountId, PagedAndSortedResultRequestDto input);
}
