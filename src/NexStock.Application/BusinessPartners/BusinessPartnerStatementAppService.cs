using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NexStock.Accounts;
using NexStock.Documents;
using NexStock.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace NexStock.BusinessPartners;

[Authorize(NexStockPermissions.BusinessPartners.Default)]
public class BusinessPartnerStatementAppService : ApplicationService, IBusinessPartnerStatementAppService
{
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<AccountTransaction, Guid> _accountTransactionRepository;
    private readonly IRepository<Account, Guid> _accountRepository;

    public BusinessPartnerStatementAppService(
        IRepository<Document, Guid> documentRepository,
        IRepository<AccountTransaction, Guid> accountTransactionRepository,
        IRepository<Account, Guid> accountRepository)
    {
        _documentRepository = documentRepository;
        _accountTransactionRepository = accountTransactionRepository;
        _accountRepository = accountRepository;
    }

    public async Task<List<BusinessPartnerPaymentHistoryDto>> GetPaymentHistoryAsync(Guid businessPartnerId)
    {
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

        return await AsyncExecuter.ToListAsync(query);
    }
}
