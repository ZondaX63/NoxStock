using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.Accounts;
using NoxStock.Permissions;
using NoxStock.MultiTenancy;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace NoxStock.Documents;

[Authorize(NoxStockPermissions.Documents.Default)]
public class PaymentAllocationAppService : ApplicationService, IPaymentAllocationAppService
{
    private readonly IRepository<PaymentAllocation, Guid> _allocationRepository;
    private readonly IRepository<AccountTransaction, Guid> _transactionRepository;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly PaymentAllocationManager _manager;

    public PaymentAllocationAppService(
        IRepository<PaymentAllocation, Guid> allocationRepository,
        IRepository<AccountTransaction, Guid> transactionRepository,
        IRepository<Document, Guid> documentRepository,
        PaymentAllocationManager manager)
    {
        _allocationRepository = allocationRepository;
        _transactionRepository = transactionRepository;
        _documentRepository = documentRepository;
        _manager = manager;
    }

    [Authorize(NoxStockPermissions.Documents.Approve)]
    [NoxStock.Licensing.TenantLicenseWrite]
    [UnitOfWork(true)]
    public async Task<PaymentAllocationDto> CreateAsync(CreatePaymentAllocationDto input)
    {
        var allocations = await CreateBatchAsync(new CreatePaymentAllocationBatchDto
        {
            AccountTransactionId = input.AccountTransactionId,
            Allocations = [new CreatePaymentAllocationLineDto
            {
                DocumentId = input.DocumentId,
                Amount = input.Amount,
                AllocationDate = input.AllocationDate
            }]
        });
        return allocations.Single();
    }

    [Authorize(NoxStockPermissions.Documents.Approve)]
    [NoxStock.Licensing.TenantLicenseWrite]
    [UnitOfWork(true)]
    public async Task<List<PaymentAllocationDto>> CreateBatchAsync(CreatePaymentAllocationBatchDto input)
    {
        CurrentTenant.RequireTenantId();
        var allocations = await _manager.CreateBatchAsync(input.AccountTransactionId,
            input.Allocations.Select(x => new PaymentAllocationLine(x.DocumentId, x.Amount, x.AllocationDate)).ToList());
        return allocations.Select(Map).ToList();
    }

    [Authorize(NoxStockPermissions.Documents.Approve)]
    [NoxStock.Licensing.TenantLicenseWrite]
    [UnitOfWork(true)]
    public async Task ReverseAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        var allocation = await _allocationRepository.GetAsync(id);
        await _manager.ReverseAsync(allocation, CurrentUser.Id);
    }

    public async Task<List<PaymentAllocationDto>> GetForDocumentAsync(Guid documentId)
    {
        CurrentTenant.RequireTenantId();
        await _documentRepository.GetAsync(documentId);
        var allocations = await _allocationRepository.GetListAsync(x => x.DocumentId == documentId);
        return allocations.OrderByDescending(x => x.AllocationDate).Select(Map).ToList();
    }

    private static PaymentAllocationDto Map(PaymentAllocation allocation) => new()
    {
        Id = allocation.Id,
        AccountTransactionId = allocation.AccountTransactionId,
        DocumentId = allocation.DocumentId,
        BusinessPartnerId = allocation.BusinessPartnerId,
        Amount = allocation.Amount,
        Currency = allocation.Currency,
        AllocationDate = allocation.AllocationDate,
        Status = allocation.Status,
        ReversedAt = allocation.ReversedAt,
        CreationTime = allocation.CreationTime,
        CreatorId = allocation.CreatorId,
        LastModificationTime = allocation.LastModificationTime,
        LastModifierId = allocation.LastModifierId
    };
}
