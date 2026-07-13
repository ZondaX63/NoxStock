using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NexStock.Permissions;
using NexStock.MultiTenancy;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace NexStock.Accounts;

[Authorize(NexStockPermissions.Accounts.Default)]
public class AccountTransactionAppService : ReadOnlyAppService<AccountTransaction, AccountTransactionDto, Guid, AccountTransactionGetListInput>, IAccountTransactionAppService
{
    public AccountTransactionAppService(IRepository<AccountTransaction, Guid> repository) 
        : base(repository)
    {
    }

    protected override async Task<IQueryable<AccountTransaction>> CreateFilteredQueryAsync(AccountTransactionGetListInput input)
    {
        return (await base.CreateFilteredQueryAsync(input))
            .WhereIf(input.AccountId.HasValue, x => x.AccountId == input.AccountId)
            .WhereIf(input.DocumentId.HasValue, x => x.DocumentId == input.DocumentId);
    }

    public override Task<AccountTransactionDto> GetAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        return base.GetAsync(id);
    }

    public override Task<PagedResultDto<AccountTransactionDto>> GetListAsync(AccountTransactionGetListInput input)
    {
        CurrentTenant.RequireTenantId();
        return base.GetListAsync(input);
    }
}
