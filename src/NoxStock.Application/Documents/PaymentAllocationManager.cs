using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NoxStock.Accounts;
using NoxStock.MultiTenancy;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace NoxStock.Documents;

public sealed record PaymentAllocationLine(Guid DocumentId, decimal Amount, DateTime AllocationDate);

public class PaymentAllocationManager : ITransientDependency
{
    private readonly IRepository<PaymentAllocation, Guid> _allocations;
    private readonly IRepository<Document, Guid> _documents;
    private readonly IRepository<AccountTransaction, Guid> _transactions;
    private readonly IPaymentAllocationLockStore _locks;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public PaymentAllocationManager(
        IRepository<PaymentAllocation, Guid> allocations,
        IRepository<Document, Guid> documents,
        IRepository<AccountTransaction, Guid> transactions,
        IPaymentAllocationLockStore locks,
        ICurrentTenant currentTenant,
        IGuidGenerator guids,
        IClock clock)
    {
        _allocations = allocations;
        _documents = documents;
        _transactions = transactions;
        _locks = locks;
        _currentTenant = currentTenant;
        _guids = guids;
        _clock = clock;
    }

    public async Task<PaymentAllocation> CreateAsync(AccountTransaction transaction, Document document, decimal amount, DateTime allocationDate)
    {
        return (await CreateBatchAsync(transaction.Id, [new PaymentAllocationLine(document.Id, amount, allocationDate)])).Single();
    }

    // Source transaction is locked before documents, which are locked by sorted ID.
    // PostgreSQL retains these locks until the ambient commercial UoW completes.
    public async Task<List<PaymentAllocation>> CreateBatchAsync(Guid accountTransactionId, IReadOnlyCollection<PaymentAllocationLine> lines)
    {
        var tenantId = _currentTenant.RequireTenantId();
        if (lines.Count == 0)
            throw new BusinessException("NoxStock:PaymentAllocationRequired");
        if (lines.Any(x => x.DocumentId == Guid.Empty || x.Amount <= 0m))
            throw new BusinessException("NoxStock:PaymentAllocationAmountMustBePositive");
        if (lines.GroupBy(x => x.DocumentId).Any(x => x.Count() > 1))
            throw new BusinessException("NoxStock:DuplicatePaymentAllocation");

        await _locks.LockAsync(tenantId, [accountTransactionId], lines.Select(x => x.DocumentId).ToArray());
        var transaction = await _transactions.GetAsync(accountTransactionId);
        var documentIds = lines.Select(x => x.DocumentId).ToArray();
        var documents = await _documents.GetListAsync(x => documentIds.Contains(x.Id));
        if (documents.Count != lines.Count)
            throw new BusinessException("NoxStock:PaymentAllocationDocumentNotFound");

        var documentById = documents.ToDictionary(x => x.Id);
        var sourceActive = await _allocations.GetListAsync(x => x.AccountTransactionId == accountTransactionId && x.Status == PaymentAllocationStatus.Active);
        if (sourceActive.Sum(x => x.Amount) + lines.Sum(x => x.Amount) > Math.Abs(transaction.Amount))
            throw new BusinessException("NoxStock:PaymentAllocationExceedsSourceAmount");

        var active = await _allocations.GetListAsync(x => documentIds.Contains(x.DocumentId) && x.Status == PaymentAllocationStatus.Active);
        if (active.Any(x => x.AccountTransactionId == accountTransactionId))
            throw new BusinessException("NoxStock:DuplicatePaymentAllocation");

        var created = new List<PaymentAllocation>(lines.Count);
        foreach (var line in lines.OrderBy(x => x.DocumentId))
        {
            var document = documentById[line.DocumentId];
            Validate(transaction, document);
            if (!await IsSourceForPartnerAsync(transaction, document.BusinessPartnerId))
                throw new BusinessException("NoxStock:PaymentAllocationBusinessPartnerMismatch");

            var documentActive = active.Where(x => x.DocumentId == document.Id).ToList();
            // The document lock makes this one-time legacy capture deterministic.
            if (documentActive.Count == 0)
                document.CaptureLegacyPaidAmountBaseline();
            var paid = (document.LegacyPaidAmountBaseline ?? 0m) + documentActive.Sum(x => x.Amount) + line.Amount;
            if (paid > document.GrandTotal)
                throw new BusinessException("NoxStock:PaymentAllocationExceedsDocumentBalance");

            var allocation = new PaymentAllocation(_guids.Create(), tenantId, transaction.Id, document.Id,
                document.BusinessPartnerId, line.Amount, document.Currency.ToUpperInvariant(),
                line.AllocationDate == default ? _clock.Now : line.AllocationDate);
            await _allocations.InsertAsync(allocation);
            document.SetPaidAmountFromAllocations(paid);
            await _documents.UpdateAsync(document);
            created.Add(allocation);
        }
        return created;
    }

    public async Task ReverseAsync(PaymentAllocation allocation, Guid? userId)
    {
        if (allocation.Status != PaymentAllocationStatus.Active)
            return;
        var tenantId = _currentTenant.RequireTenantId();
        await _locks.LockAsync(tenantId, [allocation.AccountTransactionId], [allocation.DocumentId]);
        var locked = await _allocations.GetAsync(allocation.Id);
        if (locked.Status != PaymentAllocationStatus.Active)
            return;
        await ReverseLockedAsync(locked, userId);
        await RecalculateAsync(await _documents.GetAsync(locked.DocumentId));
    }

    public async Task ReverseForTransactionAsync(Guid accountTransactionId, Guid? userId)
    {
        var tenantId = _currentTenant.RequireTenantId();
        var initial = await _allocations.GetListAsync(x => x.AccountTransactionId == accountTransactionId && x.Status == PaymentAllocationStatus.Active);
        if (initial.Count == 0) return;
        await _locks.LockAsync(tenantId, [accountTransactionId], initial.Select(x => x.DocumentId).ToArray());
        var active = await _allocations.GetListAsync(x => x.AccountTransactionId == accountTransactionId && x.Status == PaymentAllocationStatus.Active);
        foreach (var allocation in active) await ReverseLockedAsync(allocation, userId);
        foreach (var documentId in active.Select(x => x.DocumentId).Distinct()) await RecalculateAsync(await _documents.GetAsync(documentId));
    }

    public async Task ReverseForDocumentAsync(Guid documentId, Guid? userId)
    {
        var tenantId = _currentTenant.RequireTenantId();
        var initial = await _allocations.GetListAsync(x => x.DocumentId == documentId && x.Status == PaymentAllocationStatus.Active);
        if (initial.Count == 0) return;
        await _locks.LockAsync(tenantId, initial.Select(x => x.AccountTransactionId).ToArray(), [documentId]);
        var active = await _allocations.GetListAsync(x => x.DocumentId == documentId && x.Status == PaymentAllocationStatus.Active);
        foreach (var allocation in active) await ReverseLockedAsync(allocation, userId);
        await RecalculateAsync(await _documents.GetAsync(documentId));
    }

    private async Task ReverseLockedAsync(PaymentAllocation allocation, Guid? userId)
    {
        allocation.Status = PaymentAllocationStatus.Reversed;
        allocation.ReversedAt = _clock.Now;
        allocation.ReversedBy = userId;
        await _allocations.UpdateAsync(allocation);
    }

    private static void Validate(AccountTransaction transaction, Document document)
    {
        if (transaction.Status != AccountTransactionStatus.Active) throw new BusinessException("NoxStock:PaymentAllocationSourceNotActive");
        if (document.Status is not (DocumentStatus.Approved or DocumentStatus.PartiallyPaid or DocumentStatus.Paid)) throw new BusinessException("NoxStock:DocumentDoesNotAcceptAllocation");
        if (document.Type is not (DocumentType.SaleInvoice or DocumentType.PurchaseInvoice)) throw new BusinessException("NoxStock:DocumentTypeDoesNotSupportPayment");
        if (!string.Equals(transaction.Currency, document.Currency, StringComparison.OrdinalIgnoreCase)) throw new BusinessException("NoxStock:PaymentAllocationCurrencyMismatch");
        if ((document.Type == DocumentType.SaleInvoice && transaction.Type != AccountTransactionType.Collection) ||
            (document.Type == DocumentType.PurchaseInvoice && transaction.Type != AccountTransactionType.Payment)) throw new BusinessException("NoxStock:PaymentAllocationTypeMismatch");
    }

    private async Task RecalculateAsync(Document document)
    {
        var active = await _allocations.GetListAsync(x => x.DocumentId == document.Id && x.Status == PaymentAllocationStatus.Active);
        document.SetPaidAmountFromAllocations((document.LegacyPaidAmountBaseline ?? 0m) + active.Sum(x => x.Amount));
        await _documents.UpdateAsync(document);
    }

    private async Task<bool> IsSourceForPartnerAsync(AccountTransaction transaction, Guid businessPartnerId)
    {
        if (transaction.BusinessPartnerId.HasValue) return transaction.BusinessPartnerId == businessPartnerId;
        if (!transaction.DocumentId.HasValue) return false;
        return (await _documents.GetAsync(transaction.DocumentId.Value)).BusinessPartnerId == businessPartnerId;
    }
}
