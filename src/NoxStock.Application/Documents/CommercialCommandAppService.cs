using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.MultiTenancy;
using NoxStock.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace NoxStock.Documents;

[Authorize(NoxStockPermissions.Documents.Approve)]
public class CommercialCommandAppService : ApplicationService, ICommercialCommandAppService
{
    private readonly IRepository<CommercialCommand, Guid> _repository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public CommercialCommandAppService(
        IRepository<CommercialCommand, Guid> repository,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _repository = repository;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public async Task<List<CommercialCommandDto>> GetRecentAsync()
    {
        CurrentTenant.RequireTenantId();
        var query = await _repository.GetQueryableAsync();
        var commands = await AsyncExecuter.ToListAsync(query
            .OrderByDescending(x => x.CreationTime)
            .Take(100));
        return commands.Select(Map).ToList();
    }

    // Intentionally terminal only: no recovery path invokes commercial posting.
    [UnitOfWork(true)]
    public async Task<int> ExpireStaleAsync()
    {
        throw new BusinessException("NoxStock:CommandRecoveryDisabled");
    }

    private static CommercialCommandDto Map(CommercialCommand command) => new()
    {
        Id = command.Id,
        Type = command.Type,
        OperationKey = command.OperationKey,
        DocumentId = command.DocumentId,
        Status = command.Status,
        ProcessingStartedAt = command.ProcessingStartedAt,
        LeaseExpiresAt = command.LeaseExpiresAt,
        CompletedAt = command.CompletedAt,
        FailureCode = command.FailureCode
    };
}
