using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.Permissions;
using NoxStock.MultiTenancy;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace NoxStock.Accounts;

[Authorize(NoxStockPermissions.Accounts.Default)]
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
        GetPolicyName = NoxStockPermissions.Accounts.Default;
        GetListPolicyName = NoxStockPermissions.Accounts.Default;
        CreatePolicyName = NoxStockPermissions.Accounts.Create;
        UpdatePolicyName = NoxStockPermissions.Accounts.Edit;
        DeletePolicyName = NoxStockPermissions.Accounts.Delete;
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    public override async Task<AccountDto> CreateAsync(CreateUpdateAccountDto input)
    {
        var tenantId = CurrentTenant.RequireTenantId();
        await CheckCreatePolicyAsync();

        var account = new Account(
            GuidGenerator.Create(),
            tenantId,
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

    [NoxStock.Licensing.TenantLicenseWrite]
    public override async Task<AccountDto> UpdateAsync(Guid id, CreateUpdateAccountDto input)
    {
        CurrentTenant.RequireTenantId();
        await CheckUpdatePolicyAsync();

        var account = await Repository.GetAsync(id);
        account.Name = input.Name;
        account.Type = input.Type;
        account.Currency = input.Currency;
        account.IsActive = input.IsActive;

        account = await Repository.UpdateAsync(account, autoSave: true);
        return await MapToGetOutputDtoAsync(account);
    }

    [Authorize(NoxStockPermissions.Accounts.Transfer)]
    [NoxStock.Licensing.TenantLicenseWrite]
    public async Task TransferAsync(TransferDto input)
    {
        var tenantId = CurrentTenant.RequireTenantId();
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
            tenantId,
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
            tenantId,
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

    [NoxStock.Licensing.TenantLicenseWrite]
    public async Task AddTransactionAsync(AddTransactionDto input)
    {
        var tenantId = CurrentTenant.RequireTenantId();
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
            tenantId,
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
        CurrentTenant.RequireTenantId();
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

    public override Task<AccountDto> GetAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        return base.GetAsync(id);
    }

    public override Task<PagedResultDto<AccountDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        CurrentTenant.RequireTenantId();
        input.SkipCount = Math.Clamp(input.SkipCount, 0, 10_000);
        input.MaxResultCount = Math.Clamp(input.MaxResultCount, 1, 100);
        return base.GetListAsync(input);
    }

    public async Task<PagedResultDto<AccountLookupDto>> GetLookupListAsync(AccountLookupInput input)
    {
        CurrentTenant.RequireTenantId();
        await CheckGetListPolicyAsync();

        var filter = input.Filter?.Trim().ToLowerInvariant();
        var currency = input.Currency?.Trim().ToUpperInvariant();
        var query = (await Repository.GetQueryableAsync())
            .Where(account => account.IsActive)
            .WhereIf(!string.IsNullOrWhiteSpace(currency), account => account.Currency == currency)
            .WhereIf(!string.IsNullOrWhiteSpace(filter), account => account.Name.ToLower().Contains(filter!));

        var totalCount = await AsyncExecuter.CountAsync(query);
        var maxResultCount = Math.Clamp(input.MaxResultCount, 1, 30);
        var rows = await AsyncExecuter.ToListAsync(query
            .OrderBy(account => account.Name)
            .Skip(Math.Max(input.SkipCount, 0))
            .Take(maxResultCount)
            .Select(account => new AccountLookupDto
            {
                Id = account.Id,
                Name = account.Name,
                Type = account.Type,
                Currency = account.Currency,
                Balance = account.Balance
            }));

        return new PagedResultDto<AccountLookupDto>(totalCount, rows);
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    public override Task DeleteAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        return base.DeleteAsync(id);
    }
}
