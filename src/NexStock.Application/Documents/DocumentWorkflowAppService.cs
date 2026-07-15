using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NexStock.Accounts;
using NexStock.BusinessPartners;
using NexStock.Permissions;
using NexStock.Products;
using NexStock.StockMovements;
using NexStock.MultiTenancy;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace NexStock.Documents;

public class DocumentWorkflowAppService : ApplicationService, IDocumentWorkflowAppService
{
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<AccountTransaction, Guid> _accountTransactionRepository;
    private readonly IRepository<StockMovement, Guid> _stockMovementRepository;

    public DocumentWorkflowAppService(
        IRepository<Document, Guid> documentRepository,
        IRepository<Product, Guid> productRepository,
        IRepository<BusinessPartner, Guid> businessPartnerRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<AccountTransaction, Guid> accountTransactionRepository,
        IRepository<StockMovement, Guid> stockMovementRepository)
    {
        _documentRepository = documentRepository;
        _productRepository = productRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _accountRepository = accountRepository;
        _accountTransactionRepository = accountTransactionRepository;
        _stockMovementRepository = stockMovementRepository;
    }

    [Authorize(NexStockPermissions.Documents.Approve)]
    public async Task<DocumentDto> ApproveAsync(Guid id, ApproveDocumentInputDto input)
    {
        CurrentTenant.RequireTenantId();
        var document = await GetDocumentWithItemsAsync(id);

        if (document.Status is not (DocumentStatus.Draft or DocumentStatus.Sent))
        {
            throw new BusinessException("NexStock:DocumentStatusNotApprovable")
                .WithData("Status", document.Status);
        }

        if (document.Type is not (DocumentType.SaleInvoice or DocumentType.PurchaseInvoice or DocumentType.Collection or DocumentType.Payment))
        {
            throw new BusinessException("NexStock:DocumentTypeNotApprovable")
                .WithData("Type", document.Type);
        }

        if (document.Items.Count == 0 && document.Type is not (DocumentType.Collection or DocumentType.Payment))
        {
            throw new BusinessException("NexStock:DocumentRequiresItems");
        }

        var paymentAmount = document.Type is DocumentType.Collection or DocumentType.Payment
            ? document.GrandTotal
            : Math.Max(0, input.PaymentAmount);

        if (paymentAmount > document.GrandTotal)
        {
            throw new BusinessException("NexStock:PaymentExceedsDocumentTotal");
        }

        if (document.Type is DocumentType.SaleInvoice or DocumentType.PurchaseInvoice)
        {
            await ApplyStockAsync(document);
        }

        await ApplyBusinessPartnerBalanceAsync(document, paymentAmount);
        
        // For Collections/Payments, AccountId comes from the document or input. Let's assume input has it.
        await ApplyPaymentAsync(document, input.AccountId, paymentAmount, document.Date);

        document.PaidAmount = paymentAmount;
        
        if (document.Type is DocumentType.Collection or DocumentType.Payment)
        {
            document.Status = DocumentStatus.Completed;
        }
        else
        {
            if (paymentAmount >= document.GrandTotal && paymentAmount > 0)
                document.Status = DocumentStatus.Paid;
            else if (paymentAmount > 0)
                document.Status = DocumentStatus.PartiallyPaid;
            else
                document.Status = DocumentStatus.Approved;
        }

        await _documentRepository.UpdateAsync(document, autoSave: true);
        return await MapDocumentAsync(document);
    }

    [Authorize(NexStockPermissions.Documents.Approve)]
    public async Task<DocumentDto> RecordPaymentAsync(Guid id, RecordDocumentPaymentInputDto input)
    {
        CurrentTenant.RequireTenantId();
        var document = await GetDocumentWithItemsAsync(id);

        if (document.Type is not (DocumentType.SaleInvoice or DocumentType.PurchaseInvoice))
        {
            throw new BusinessException("NexStock:DocumentTypeDoesNotSupportPayment");
        }

        if (document.Status is not (DocumentStatus.Approved or DocumentStatus.PartiallyPaid))
        {
            throw new BusinessException("NexStock:DocumentStatusDoesNotAcceptPayment")
                .WithData("Status", document.Status);
        }

        if (input.Amount <= 0)
        {
            throw new BusinessException("NexStock:PaymentAmountMustBePositive");
        }

        var outstandingAmount = document.GrandTotal - document.PaidAmount;
        if (input.Amount > outstandingAmount)
        {
            throw new BusinessException("NexStock:PaymentExceedsOutstandingAmount")
                .WithData("OutstandingAmount", outstandingAmount);
        }

        var businessPartner = await _businessPartnerRepository.GetAsync(document.BusinessPartnerId);
        if (document.Type == DocumentType.SaleInvoice)
        {
            businessPartner.Balance -= input.Amount;
        }
        else
        {
            businessPartner.Balance += input.Amount;
        }

        await _businessPartnerRepository.UpdateAsync(businessPartner);
        await ApplyPaymentAsync(document, input.AccountId, input.Amount, input.PaymentDate, input.Description);

        document.PaidAmount += input.Amount;
        document.Status = document.PaidAmount >= document.GrandTotal
            ? DocumentStatus.Paid
            : DocumentStatus.PartiallyPaid;

        await _documentRepository.UpdateAsync(document, autoSave: true);
        return await MapDocumentAsync(document);
    }

    [Authorize(NexStockPermissions.Documents.Cancel)]
    public async Task<DocumentDto> CancelAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        var document = await GetDocumentWithItemsAsync(id);

        if (document.Status is not (DocumentStatus.Approved or DocumentStatus.Paid or DocumentStatus.Completed or DocumentStatus.PartiallyPaid))
        {
            throw new BusinessException("NexStock:DocumentStatusNotCancellable")
                .WithData("Status", document.Status);
        }

        await ReverseStockAsync(document);
        await ReverseBusinessPartnerBalanceAsync(document);
        await ReversePaymentsAsync(document);

        document.Status = DocumentStatus.Cancelled;
        await _documentRepository.UpdateAsync(document, autoSave: true);

        return await MapDocumentAsync(document);
    }

    private async Task<Document> GetDocumentWithItemsAsync(Guid id)
    {
        var queryable = await _documentRepository.WithDetailsAsync(x => x.Items);
        var document = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Id == id));

        if (document is null)
        {
            throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Document), id);
        }

        return document;
    }

    private async Task ApplyStockAsync(Document document)
    {
        foreach (var item in document.Items)
        {
            var product = await _productRepository.GetAsync(item.ProductId);
            if (!product.TrackStock)
            {
                continue;
            }

            var quantityDelta = document.Type == DocumentType.SaleInvoice
                ? -item.Quantity
                : item.Quantity;

            product.Quantity += quantityDelta;
            await _productRepository.UpdateAsync(product);

            await _stockMovementRepository.InsertAsync(
                new StockMovement(
                    GuidGenerator.Create(),
                    CurrentTenant.Id,
                    product.Id,
                    document.Type == DocumentType.SaleInvoice
                        ? StockMovementType.SalesInvoice
                        : StockMovementType.PurchaseInvoice,
                    quantityDelta,
                    product.Quantity,
                    document.Date,
                    document.Id,
                    $"{document.DocNumber} onay hareketi"
                )
            );
        }
    }

    private async Task ReverseStockAsync(Document document)
    {
        var movements = await _stockMovementRepository.GetListAsync(x =>
            x.DocumentId == document.Id &&
            x.Type != StockMovementType.Reversal);

        foreach (var movement in movements)
        {
            var product = await _productRepository.GetAsync(movement.ProductId);
            var quantityDelta = -movement.Quantity;
            product.Quantity += quantityDelta;
            await _productRepository.UpdateAsync(product);

            await _stockMovementRepository.InsertAsync(
                new StockMovement(
                    GuidGenerator.Create(),
                    CurrentTenant.Id,
                    product.Id,
                    StockMovementType.Reversal,
                    quantityDelta,
                    product.Quantity,
                    Clock.Now,
                    document.Id,
                    $"{document.DocNumber} iptal stok ters kaydı"
                )
            );
        }
    }

    private async Task ApplyBusinessPartnerBalanceAsync(Document document, decimal paymentAmount)
    {
        var businessPartner = await _businessPartnerRepository.GetAsync(document.BusinessPartnerId);

        if (document.Type == DocumentType.SaleInvoice)
        {
            businessPartner.Balance += document.GrandTotal - paymentAmount;
        }
        else if (document.Type == DocumentType.PurchaseInvoice)
        {
            businessPartner.Balance -= document.GrandTotal - paymentAmount;
        }
        else if (document.Type == DocumentType.Collection)
        {
            businessPartner.Balance -= document.GrandTotal;
        }
        else if (document.Type == DocumentType.Payment)
        {
            businessPartner.Balance += document.GrandTotal;
        }

        await _businessPartnerRepository.UpdateAsync(businessPartner);
    }

    private async Task ReverseBusinessPartnerBalanceAsync(Document document)
    {
        var businessPartner = await _businessPartnerRepository.GetAsync(document.BusinessPartnerId);

        if (document.Type == DocumentType.SaleInvoice)
        {
            businessPartner.Balance -= document.GrandTotal - document.PaidAmount;
        }
        else if (document.Type == DocumentType.PurchaseInvoice)
        {
            businessPartner.Balance += document.GrandTotal - document.PaidAmount;
        }
        else if (document.Type == DocumentType.Collection)
        {
            businessPartner.Balance += document.GrandTotal;
        }
        else if (document.Type == DocumentType.Payment)
        {
            businessPartner.Balance -= document.GrandTotal;
        }

        await _businessPartnerRepository.UpdateAsync(businessPartner);
    }

    private async Task ApplyPaymentAsync(
        Document document,
        Guid? accountId,
        decimal paymentAmount,
        DateTime transactionDate,
        string? description = null)
    {
        if (paymentAmount <= 0)
        {
            return;
        }

        if (!accountId.HasValue)
        {
            throw new BusinessException("NexStock:PaymentRequiresAccount");
        }

        var account = await _accountRepository.GetAsync(accountId.Value);
        if (!string.Equals(account.Currency, document.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("NexStock:PaymentAccountCurrencyDoesNotMatchDocument")
                .WithData("AccountCurrency", account.Currency)
                .WithData("DocumentCurrency", document.Currency);
        }
        
        decimal amountDelta;
        AccountTransactionType transactionType;

        if (document.Type is DocumentType.SaleInvoice or DocumentType.Collection)
        {
            amountDelta = paymentAmount;
            transactionType = AccountTransactionType.Collection;
        }
        else
        {
            amountDelta = -paymentAmount;
            transactionType = AccountTransactionType.Payment;
        }

        account.Balance += amountDelta;
        await _accountRepository.UpdateAsync(account);

        await _accountTransactionRepository.InsertAsync(
            new AccountTransaction(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                account.Id,
                transactionType,
                amountDelta,
                document.Currency,
                transactionDate,
                document.Id,
                description ?? $"{document.DocNumber} ödeme hareketi"
            )
        );
    }

    private async Task ReversePaymentsAsync(Document document)
    {
        var transactions = await _accountTransactionRepository.GetListAsync(x =>
            x.DocumentId == document.Id &&
            x.Type != AccountTransactionType.Reversal);

        foreach (var transaction in transactions)
        {
            var account = await _accountRepository.GetAsync(transaction.AccountId);
            var amountDelta = -transaction.Amount;
            account.Balance += amountDelta;
            await _accountRepository.UpdateAsync(account);

            await _accountTransactionRepository.InsertAsync(
                new AccountTransaction(
                    GuidGenerator.Create(),
                    CurrentTenant.Id,
                    account.Id,
                    AccountTransactionType.Reversal,
                    amountDelta,
                    transaction.Currency,
                    Clock.Now,
                    document.Id,
                    $"{document.DocNumber} iptal ödeme ters kaydı"
                )
            );
        }
    }

    private async Task<DocumentDto> MapDocumentAsync(Document document)
    {
        var dto = ObjectMapper.Map<Document, DocumentDto>(document);
        var businessPartner = await _businessPartnerRepository.FindAsync(document.BusinessPartnerId);
        dto.BusinessPartnerName = businessPartner?.Name;
        return dto;
    }
}
