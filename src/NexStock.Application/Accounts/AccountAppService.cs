using System;
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
}
