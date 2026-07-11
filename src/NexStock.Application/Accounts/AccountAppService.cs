using System;
using System.Linq;
using System.Threading.Tasks;
using NexStock.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace NexStock.Accounts;

public class AccountAppService :
    CrudAppService<
        Account,
        AccountDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateAccountDto>,
    IAccountAppService
{
    public AccountAppService(IRepository<Account, Guid> repository)
        : base(repository)
    {
        GetPolicyName = NexStockPermissions.Accounts.Default;
        GetListPolicyName = NexStockPermissions.Accounts.Default;
        CreatePolicyName = NexStockPermissions.Accounts.Create;
        UpdatePolicyName = NexStockPermissions.Accounts.Edit;
        DeletePolicyName = NexStockPermissions.Accounts.Delete;
    }

    public override async Task<AccountDto> CreateAsync(CreateUpdateAccountDto input)
    {
        await CheckCreatePolicyAsync();

        var account = new Account(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            input.Name,
            input.Type,
            input.Currency
        )
        {
            Balance = input.Balance,
            IsActive = input.IsActive
        };

        account = await Repository.InsertAsync(account, autoSave: true);
        return await MapToGetOutputDtoAsync(account);
    }

    public override async Task<AccountDto> UpdateAsync(Guid id, CreateUpdateAccountDto input)
    {
        await CheckUpdatePolicyAsync();

        var account = await Repository.GetAsync(id);
        account.Name = input.Name;
        account.Type = input.Type;
        account.Currency = input.Currency;
        account.IsActive = input.IsActive;

        account = await Repository.UpdateAsync(account, autoSave: true);
        return await MapToGetOutputDtoAsync(account);
    }

    public async Task TransferAsync(TransferDto input)
    {
        await CheckUpdatePolicyAsync();

        if (input.FromAccountId == input.ToAccountId)
        {
            throw new Volo.Abp.UserFriendlyException("Kaynak ve hedef hesap aynı olamaz.");
        }

        var fromAccount = await Repository.GetAsync(input.FromAccountId);
        var toAccount = await Repository.GetAsync(input.ToAccountId);

        if (fromAccount.Balance < input.Amount)
        {
            throw new Volo.Abp.UserFriendlyException("Kaynak hesapta yeterli bakiye bulunmuyor.");
        }

        fromAccount.Balance -= input.Amount;
        toAccount.Balance += input.Amount;

        var transactionRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<AccountTransaction, Guid>>();

        await transactionRepository.InsertAsync(new AccountTransaction(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            fromAccount.Id,
            AccountTransactionType.TransferOut,
            input.Amount,
            fromAccount.Currency,
            input.Date,
            null,
            input.Description ?? $"Transfer to {toAccount.Name}"
        ));

        await transactionRepository.InsertAsync(new AccountTransaction(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            toAccount.Id,
            AccountTransactionType.TransferIn,
            input.Amount,
            toAccount.Currency,
            input.Date,
            null,
            input.Description ?? $"Transfer from {fromAccount.Name}"
        ));

        await Repository.UpdateAsync(fromAccount);
        await Repository.UpdateAsync(toAccount);
    }

    public async Task AddTransactionAsync(AddTransactionDto input)
    {
        await CheckUpdatePolicyAsync();

        var account = await Repository.GetAsync(input.AccountId);

        if (input.Type == AccountTransactionType.Income || input.Type == AccountTransactionType.Collection)
        {
            account.Balance += input.Amount;
        }
        else if (input.Type == AccountTransactionType.Expense || input.Type == AccountTransactionType.Payment || input.Type == AccountTransactionType.TransferOut)
        {
            account.Balance -= input.Amount;
        }

        var transactionRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<AccountTransaction, Guid>>();

        await transactionRepository.InsertAsync(new AccountTransaction(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            account.Id,
            input.Type,
            input.Amount,
            account.Currency,
            input.Date,
            null,
            input.Description
        ));

        await Repository.UpdateAsync(account);
    }

    public async Task<PagedResultDto<AccountTransactionDto>> GetTransactionsAsync(Guid accountId, PagedAndSortedResultRequestDto input)
    {
        await CheckGetPolicyAsync();
        var transactionRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<AccountTransaction, Guid>>();
        
        var query = await transactionRepository.GetQueryableAsync();
        query = query.Where(t => t.AccountId == accountId);

        var totalCount = await AsyncExecuter.CountAsync(query);

        var transactions = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(t => t.TransactionDate)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount)
        );

        return new PagedResultDto<AccountTransactionDto>(
            totalCount,
            ObjectMapper.Map<System.Collections.Generic.List<AccountTransaction>, System.Collections.Generic.List<AccountTransactionDto>>(transactions)
        );
    }
}
