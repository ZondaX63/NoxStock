using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.Accounts;
using NoxStock.BusinessPartnerTransfers;
using NoxStock.Documents;
using NoxStock.Permissions;
using NoxStock.MultiTenancy;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace NoxStock.BusinessPartners;

[Authorize(NoxStockPermissions.BusinessPartners.Default)]
public class BusinessPartnerStatementAppService : ApplicationService, IBusinessPartnerStatementAppService
{
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<AccountTransaction, Guid> _accountTransactionRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IRepository<BusinessPartnerTransfer, Guid> _transferRepository;

    public BusinessPartnerStatementAppService(
        IRepository<Document, Guid> documentRepository,
        IRepository<AccountTransaction, Guid> accountTransactionRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<BusinessPartner, Guid> businessPartnerRepository,
        IRepository<BusinessPartnerTransfer, Guid> transferRepository)
    {
        _documentRepository = documentRepository;
        _accountTransactionRepository = accountTransactionRepository;
        _accountRepository = accountRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _transferRepository = transferRepository;
    }

    public async Task<List<BusinessPartnerPaymentHistoryDto>> GetPaymentHistoryAsync(Guid businessPartnerId)
    {
        CurrentTenant.RequireTenantId();
        var documents = await _documentRepository.GetQueryableAsync();
        var transactions = await _accountTransactionRepository.GetQueryableAsync();
        var accounts = await _accountRepository.GetQueryableAsync();

        var query =
            from transaction in transactions
            where transaction.DocumentId.HasValue &&
                  (transaction.Type == AccountTransactionType.Collection ||
                   transaction.Type == AccountTransactionType.Payment ||
                   transaction.Type == AccountTransactionType.Reversal)
            join document in documents on transaction.DocumentId!.Value equals document.Id
            join account in accounts on transaction.AccountId equals account.Id
            where document.BusinessPartnerId == businessPartnerId
            orderby transaction.TransactionDate descending, transaction.CreationTime descending
            select new BusinessPartnerPaymentHistoryDto
            {
                Id = transaction.Id,
                DocumentId = document.Id,
                DocumentNumber = document.DocNumber,
                DocumentType = document.Type,
                AccountName = account.Name,
                TransactionType = transaction.Type,
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                TransactionDate = transaction.TransactionDate,
                Description = transaction.Description
            };

        var paymentHistory = await AsyncExecuter.ToListAsync(query);
        var transfers = await _transferRepository.GetQueryableAsync();
        var relatedTransfers = await AsyncExecuter.ToListAsync(
            transfers.Where(x => x.CustomerId == businessPartnerId || x.SupplierId == businessPartnerId));

        if (!relatedTransfers.Any())
            return paymentHistory;

        var counterpartyIds = relatedTransfers
            .Select(x => x.CustomerId == businessPartnerId ? x.SupplierId : x.CustomerId)
            .Distinct()
            .ToList();
        var partners = await _businessPartnerRepository.GetQueryableAsync();
        var counterpartyRows = await AsyncExecuter.ToListAsync(
            partners.Where(x => counterpartyIds.Contains(x.Id)));
        var counterpartyNames = counterpartyRows.ToDictionary(x => x.Id, x => x.Name);

        paymentHistory.AddRange(relatedTransfers.Select(transfer =>
        {
            var isCustomerSide = transfer.CustomerId == businessPartnerId;
            var counterpartyId = isCustomerSide ? transfer.SupplierId : transfer.CustomerId;
            return new BusinessPartnerPaymentHistoryDto
            {
                Id = transfer.Id,
                DocumentId = Guid.Empty,
                DocumentNumber = transfer.ReferenceNumber,
                AccountName = "Cari virman",
                TransactionType = AccountTransactionType.TransferOut,
                Amount = isCustomerSide ? -transfer.Amount : transfer.Amount,
                Currency = transfer.Currency,
                TransactionDate = transfer.TransferDate,
                Description = transfer.Description,
                IsBusinessPartnerTransfer = true,
                CounterpartyName = counterpartyNames.GetValueOrDefault(counterpartyId)
            };
        }));

        return paymentHistory
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.Id)
            .ToList();
    }
}
