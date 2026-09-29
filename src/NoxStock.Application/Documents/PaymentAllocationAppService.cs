using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
    private readonly CommercialCommandCoordinator _commands;

    public PaymentAllocationAppService(
        IRepository<PaymentAllocation, Guid> allocationRepository,
        IRepository<AccountTransaction, Guid> transactionRepository,
        IRepository<Document, Guid> documentRepository,
        PaymentAllocationManager manager, CommercialCommandCoordinator commands)
    {
        _allocationRepository = allocationRepository;
        _transactionRepository = transactionRepository;
        _documentRepository = documentRepository;
        _manager = manager; _commands = commands;
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
            }], OperationKey = input.OperationKey
        });
        return allocations.Single();
    }

    [Authorize(NoxStockPermissions.Documents.Approve)]
    [NoxStock.Licensing.TenantLicenseWrite]
    [UnitOfWork(true)]
    public async Task<List<PaymentAllocationDto>> CreateBatchAsync(CreatePaymentAllocationBatchDto input)
    {
        CurrentTenant.RequireTenantId();
        var operationKey = Normalize(input.OperationKey);
        var lines = input.Allocations.Select(x => new PaymentAllocationLine(x.DocumentId, x.Amount, x.AllocationDate)).ToList();
        var allocations = await _commands.ExecuteAsync(CurrentTenant.Id, CommercialCommandType.PaymentAllocationBatch,
            operationKey, input.AccountTransactionId,
            () => _manager.CreateBatchAsync(input.AccountTransactionId, lines, operationKey),
            async _ => await _allocationRepository.GetListAsync(x => x.AccountTransactionId == input.AccountTransactionId && x.OperationKey == operationKey),
            _ => input.AccountTransactionId, BatchFingerprint(input.AccountTransactionId, lines));
        return allocations.Select(Map).ToList();
    }

    [Authorize(NoxStockPermissions.Documents.Approve)]
    [NoxStock.Licensing.TenantLicenseWrite]
    [UnitOfWork(true)]
    public async Task ReverseAsync(Guid id, ReversePaymentAllocationDto input)
    {
        CurrentTenant.RequireTenantId();
        var operationKey = Normalize(input.OperationKey);
        await _commands.ExecuteAsync(CurrentTenant.Id, CommercialCommandType.PaymentAllocationReversal, operationKey, id,
            async () => { var allocation = await _allocationRepository.GetAsync(id); await _manager.ReverseAsync(allocation, CurrentUser.Id); return allocation; },
            async _ => await _allocationRepository.GetAsync(id), _ => id, $"reverse:{id:N}");
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
        OperationKey = allocation.OperationKey,
        CreationTime = allocation.CreationTime,
        CreatorId = allocation.CreatorId,
        LastModificationTime = allocation.LastModificationTime,
        LastModifierId = allocation.LastModifierId
    };

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > 100)
            throw new BusinessException("NoxStock:OperationKeyTooLong");
        return normalized;
    }

    private static string BatchFingerprint(Guid transactionId, IEnumerable<PaymentAllocationLine> lines)
    {
        var payload = transactionId.ToString("N") + "|" + string.Join("|", lines.OrderBy(x => x.DocumentId)
            .Select(x => $"{x.DocumentId:N}:{x.Amount:0.####}:{x.AllocationDate:O}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
