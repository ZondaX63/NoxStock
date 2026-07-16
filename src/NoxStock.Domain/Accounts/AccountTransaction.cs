using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NoxStock.Accounts;

public class AccountTransaction : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    public Guid AccountId { get; set; }
    public Guid? DocumentId { get; set; }
    public AccountTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Description { get; set; }
    public string? OperationKey { get; set; }

    protected AccountTransaction()
    {
        Currency = "TRY";
        TransactionDate = DateTime.UtcNow;
    }

    public AccountTransaction(
        Guid id,
        Guid? tenantId,
        Guid accountId,
        AccountTransactionType type,
        decimal amount,
        string currency,
        DateTime transactionDate,
        Guid? documentId = null,
        string? description = null,
        string? operationKey = null)
        : base(id)
    {
        TenantId = tenantId;
        AccountId = accountId;
        DocumentId = documentId;
        Type = type;
        Amount = amount;
        Currency = currency;
        TransactionDate = transactionDate;
        Description = description;
        OperationKey = operationKey;
    }
}
