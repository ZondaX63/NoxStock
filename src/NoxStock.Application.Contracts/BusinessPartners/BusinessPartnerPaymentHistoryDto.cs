using System;
using NoxStock.Accounts;
using NoxStock.Documents;

namespace NoxStock.BusinessPartners;

public class BusinessPartnerPaymentHistoryDto
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public DocumentType DocumentType { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public AccountTransactionType TransactionType { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public DateTime TransactionDate { get; set; }
    public string? Description { get; set; }
}
