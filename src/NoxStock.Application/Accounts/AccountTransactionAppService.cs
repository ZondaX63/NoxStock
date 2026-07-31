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
        input.SkipCount = Math.Clamp(input.SkipCount, 0, 10_000);
        input.MaxResultCount = Math.Clamp(input.MaxResultCount, 1, 100);
        return base.GetListAsync(input);
    }
}
