using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.Accounts;
using NoxStock.BusinessPartners;
using NoxStock.Permissions;
using NoxStock.Products;
using NoxStock.ExchangeRates;
using NoxStock.StockMovements;
using NoxStock.MultiTenancy;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace NoxStock.Documents;

public class DocumentWorkflowAppService : ApplicationService, IDocumentWorkflowAppService
{
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<AccountTransaction, Guid> _accountTransactionRepository;
    private readonly IRepository<StockMovement, Guid> _stockMovementRepository;
    private readonly IProductCostProfileService _productCostProfileService;
    private readonly IProductPriceCalculator _productPriceCalculator;
    private readonly IRepository<ExchangeRate, Guid> _exchangeRateRepository;
    private readonly IRepository<DocumentExchangeRateSnapshot, Guid> _exchangeRateSnapshotRepository;

    public DocumentWorkflowAppService(
        IRepository<Document, Guid> documentRepository,
        IRepository<Product, Guid> productRepository,
        IRepository<BusinessPartner, Guid> businessPartnerRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<AccountTransaction, Guid> accountTransactionRepository,
        IRepository<StockMovement, Guid> stockMovementRepository,
        IProductCostProfileService productCostProfileService,
        IProductPriceCalculator productPriceCalculator,
        IRepository<ExchangeRate, Guid> exchangeRateRepository,
        IRepository<DocumentExchangeRateSnapshot, Guid> exchangeRateSnapshotRepository)
    {
        _documentRepository = documentRepository;
        _productRepository = productRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _accountRepository = accountRepository;
        _accountTransactionRepository = accountTransactionRepository;
        _stockMovementRepository = stockMovementRepository;
        _productCostProfileService = productCostProfileService;
        _productPriceCalculator = productPriceCalculator;
        _exchangeRateRepository = exchangeRateRepository;
        _exchangeRateSnapshotRepository = exchangeRateSnapshotRepository;
    }

    [Authorize(NoxStockPermissions.Documents.Approve)]
    [UnitOfWork(true)]
    [NoxStock.Licensing.TenantLicenseWrite]
    public async Task<DocumentDto> ApproveAsync(Guid id, ApproveDocumentInputDto input)
    {
        CurrentTenant.RequireTenantId();
        var document = await GetDocumentWithItemsAsync(id);

        if (document.Status is not (DocumentStatus.Draft or DocumentStatus.Sent))
        {
            throw new BusinessException("NoxStock:DocumentStatusNotApprovable")
                .WithData("Status", document.Status);
        }

        if (document.Type is not (DocumentType.SaleInvoice or DocumentType.PurchaseInvoice or DocumentType.Collection or DocumentType.Payment))
        {
            throw new BusinessException("NoxStock:DocumentTypeNotApprovable")
                .WithData("Type", document.Type);
        }

        if (document.Items.Count == 0 && document.Type is not (DocumentType.Collection or DocumentType.Payment))
        {
            throw new BusinessException("NoxStock:DocumentRequiresItems");
        }

        if (document.Type == DocumentType.SaleInvoice)
        {
            await LockSalesPricingSnapshotsAsync(document, input.ConfirmBelowCost);
        }

        var paymentAmount = document.Type is DocumentType.Collection or DocumentType.Payment
            ? document.GrandTotal
            : Math.Max(0, input.PaymentAmount);

        if (paymentAmount > document.GrandTotal)
        {
            throw new BusinessException("NoxStock:PaymentExceedsDocumentTotal");
        }

        await EnsureBusinessPartnerCreditLimitAsync(document, paymentAmount);

        if (document.Type is DocumentType.SaleInvoice or DocumentType.PurchaseInvoice)
        {
            await ApplyStockAsync(document);
        }

        if (document.Type == DocumentType.PurchaseInvoice)
        {
            await RefreshPurchaseRatesAtApprovalAsync(document);
            await _productCostProfileService.ApplyApprovedPurchaseAsync(document);
        }

        await ApplyBusinessPartnerBalanceAsync(document, paymentAmount);
        
        // For Collections/Payments, AccountId comes from the document or input. Let's assume input has it.
        await ApplyPaymentAsync(document, input.AccountId, paymentAmount, document.Date);

        document.PaidAmount = paymentAmount;
        
        var targetStatus = document.Type is DocumentType.Collection or DocumentType.Payment
            ? DocumentStatus.Completed
            : paymentAmount >= document.GrandTotal && paymentAmount > 0
                ? DocumentStatus.Paid
                : paymentAmount > 0
                    ? DocumentStatus.PartiallyPaid
                    : DocumentStatus.Approved;
        document.TransitionTo(targetStatus);

        await _documentRepository.UpdateAsync(document, autoSave: true);
        return await MapDocumentAsync(document);
    }

    [Authorize(NoxStockPermissions.Documents.Approve)]
    [UnitOfWork(true)]
    [NoxStock.Licensing.TenantLicenseWrite]
    public async Task<DocumentDto> RecordPaymentAsync(Guid id, RecordDocumentPaymentInputDto input)
    {
        CurrentTenant.RequireTenantId();
        var document = await GetDocumentWithItemsAsync(id);
        var operationKey = NormalizeOperationKey(input.OperationKey);

        if (operationKey is not null && await _accountTransactionRepository.AnyAsync(x =>
                x.DocumentId == document.Id && x.OperationKey == operationKey))
        {
            return await MapDocumentAsync(document);
        }

        if (document.Type is not (DocumentType.SaleInvoice or DocumentType.PurchaseInvoice))
        {
            throw new BusinessException("NoxStock:DocumentTypeDoesNotSupportPayment");
        }

        if (document.Status is not (DocumentStatus.Approved or DocumentStatus.PartiallyPaid))
        {
            throw new BusinessException("NoxStock:DocumentStatusDoesNotAcceptPayment")
                .WithData("Status", document.Status);
        }

        if (input.Amount <= 0)
        {
            throw new BusinessException("NoxStock:PaymentAmountMustBePositive");
        }

        var outstandingAmount = document.GrandTotal - document.PaidAmount;
        if (input.Amount > outstandingAmount)
        {
            throw new BusinessException("NoxStock:PaymentExceedsOutstandingAmount")
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
        await ApplyPaymentAsync(document, input.AccountId, input.Amount, input.PaymentDate, input.Description, operationKey);

        document.PaidAmount += input.Amount;
        document.TransitionTo(document.PaidAmount >= document.GrandTotal
            ? DocumentStatus.Paid
            : DocumentStatus.PartiallyPaid);

        await _documentRepository.UpdateAsync(document, autoSave: true);
        return await MapDocumentAsync(document);
    }

    [Authorize(NoxStockPermissions.Documents.Cancel)]
    [UnitOfWork(true)]
    [NoxStock.Licensing.TenantLicenseWrite]
    public async Task<DocumentDto> CancelAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        var document = await GetDocumentWithItemsAsync(id);

        if (document.Status is not (DocumentStatus.Approved or DocumentStatus.Paid or DocumentStatus.Completed or DocumentStatus.PartiallyPaid))
        {
            throw new BusinessException("NoxStock:DocumentStatusNotCancellable")
                .WithData("Status", document.Status);
        }

        await ReverseStockAsync(document);
        if (document.Type == DocumentType.PurchaseInvoice)
        {
            await _productCostProfileService.ReverseApprovedPurchaseAsync(document);
        }
        await ReverseBusinessPartnerBalanceAsync(document);
        await ReversePaymentsAsync(document);

        document.TransitionTo(DocumentStatus.Cancelled);
        await _documentRepository.UpdateAsync(document, autoSave: true);

        return await MapDocumentAsync(document);
    }

    private async Task<Document> GetDocumentWithItemsAsync(Guid id)
    {
        var queryable = await _documentRepository.WithDetailsAsync(x => x.Items, x => x.ExchangeRateSnapshots);
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

    private async Task LockSalesPricingSnapshotsAsync(Document document, bool confirmBelowCost)
    {
        if (!string.Equals(document.Currency, "TRY", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("NoxStock:SalesDocumentsRequireTry");
        }

        var businessPartner = await _businessPartnerRepository.GetAsync(document.BusinessPartnerId);
        ProductPriceCalculation? firstCalculation = null;
        foreach (var item in document.Items)
        {
            var product = await _productRepository.GetAsync(item.ProductId);
            var calculation = await _productPriceCalculator.CalculateAsync(product);
            firstCalculation ??= calculation;

            var usesDynamicPricing = product.PricingMode == ProductPricingMode.DynamicCurrencyMarkup;
            if (usesDynamicPricing && !calculation.IsAvailable)
            {
                throw new BusinessException(calculation.UnavailableReason ?? "NoxStock:ProductApprovedPurchaseCostUnavailable")
                    .WithData("ProductName", product.Name);
            }

            if (usesDynamicPricing && !item.IsUnitPriceOverridden && calculation.IsAvailable)
            {
                item.UnitPrice = calculation.ListSalePriceInTry;
            }

            ApplyBusinessPartnerSalesDiscountDefaults(item, businessPartner);
            if (item.VatRate == 0m && product.VatRate > 0)
            {
                item.VatRate = product.VatRate;
            }

            item.PriceCurrencyCode = calculation.Currency;
            item.PriceExchangeRate = calculation.ExchangeRate;
            item.PriceRateDate = calculation.ReferenceCurrencyRateDate ?? calculation.RateDate;
            item.UnitPriceTry = RoundMoney(item.UnitPrice);
            item.ProductCostHistoryId = calculation.ProductCostHistoryId ?? product.LastPurchaseCostHistoryId;
            item.PricingMode = item.IsUnitPriceOverridden ? ProductPricingMode.Manual : product.PricingMode;
            item.CostCurrencyCode = product.LastPurchaseCostCurrencyCode;
            item.CostCurrencyAmount = product.LastPurchaseCostCurrencyAmount;
            item.CostTryAmount = calculation.NetCostInTry > 0m
                ? calculation.NetCostInTry
                : product.LastPurchaseCostTryAmount;
            item.AppliedMarkupPercent = calculation.MarkupPercent;
            item.ReferenceCurrencyCode = calculation.ReferenceCurrencyCode;
            item.ReferenceUnitPrice = calculation.ReferenceSaleUnitPrice;
            item.ReferenceExchangeRate = calculation.ReferenceCurrencyExchangeRate;
            item.PricingSnapshotDate = Clock.Now;

            item.DiscountRate = GetEffectiveSalesDiscountRate(item);
            var lineNet = item.Quantity * item.UnitPrice;
            var discount = lineNet * item.DiscountRate / 100m;
            var taxable = lineNet - discount;
            var netUnitSale = item.Quantity > 0m ? taxable / item.Quantity : 0m;
            if (calculation.NetCostInTry > 0m && netUnitSale < calculation.NetCostInTry)
            {
                var canOverride = await AuthorizationService.IsGrantedAsync(NoxStockPermissions.Documents.SellBelowCost);
                if (!confirmBelowCost || !canOverride)
                {
                    throw new BusinessException("NoxStock:SaleBelowInventoryCost")
                        .WithData("ProductName", product.Name)
                        .WithData("InventoryCost", calculation.NetCostInTry)
                        .WithData("NetSalePrice", RoundMoney(netUnitSale))
                        .WithData("CanOverride", canOverride);
                }
            }
            item.Total = RoundMoney(taxable + taxable * item.VatRate / 100m);
            item.TotalCostTryAmount = RoundMoney((item.CostTryAmount ?? 0m) * item.Quantity);
        }

        if (firstCalculation is not null)
        {
            document.Currency = "TRY";
            document.ExchangeRate = 1m;
        }

        document.Subtotal = RoundMoney(document.Items.Sum(x => x.Quantity * x.UnitPrice));
        document.DiscountTotal = RoundMoney(document.Items.Sum(x => x.Quantity * x.UnitPrice * x.DiscountRate / 100m));
        document.VatTotal = RoundMoney(document.Items.Sum(x =>
        {
            var lineNet = x.Quantity * x.UnitPrice;
            var discount = lineNet * x.DiscountRate / 100m;
            return (lineNet - discount) * x.VatRate / 100m;
        }));
        document.GrandTotal = RoundMoney(document.Subtotal - document.DiscountTotal + document.VatTotal);
    }

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static void ApplyBusinessPartnerSalesDiscountDefaults(DocumentItem item, BusinessPartner businessPartner)
    {
        var hasLineDiscounts = item.SalesDiscount1 != 0m || item.SalesDiscount2 != 0m ||
            item.SalesDiscount3 != 0m || item.SalesDiscount4 != 0m || item.DiscountRate != 0m;
        if (hasLineDiscounts)
        {
            return;
        }

        item.SalesDiscount1 = businessPartner.DefaultSalesDiscount1;
        item.SalesDiscount2 = businessPartner.DefaultSalesDiscount2;
        item.SalesDiscount3 = businessPartner.DefaultSalesDiscount3;
        item.SalesDiscount4 = businessPartner.DefaultSalesDiscount4;
    }

    private static decimal GetEffectiveSalesDiscountRate(DocumentItem item)
    {
        var hasDiscountChain = item.SalesDiscount1 != 0m || item.SalesDiscount2 != 0m ||
            item.SalesDiscount3 != 0m || item.SalesDiscount4 != 0m;
        if (!hasDiscountChain)
        {
            return item.DiscountRate;
        }

        var remainingFactor = (1m - item.SalesDiscount1 / 100m) *
            (1m - item.SalesDiscount2 / 100m) *
            (1m - item.SalesDiscount3 / 100m) *
            (1m - item.SalesDiscount4 / 100m);
        return RoundMoney((1m - remainingFactor) * 100m);
    }

    private async Task RefreshPurchaseRatesAtApprovalAsync(Document document)
    {
        // Currency snapshots are created while the dated draft is saved.
        // Approval validates those snapshots but must not rewrite them with
        // approval-day exchange rates.
        foreach (var item in document.Items)
        {
            var product = await _productRepository.GetAsync(item.ProductId);
            var referenceCurrency = string.IsNullOrWhiteSpace(product.PurchaseCurrency)
                ? "TRY"
                : product.PurchaseCurrency.Trim().ToUpperInvariant();
            foreach (var currency in new[] { "TRY", document.Currency, referenceCurrency }
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var snapshot = document.ExchangeRateSnapshots.FirstOrDefault(x =>
                    string.Equals(x.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase));
                if (snapshot is null || snapshot.RateToTry <= 0m)
                {
                    throw new BusinessException("NoxStock:DocumentExchangeRateSnapshotUnavailable")
                        .WithData("Currency", currency);
                }
            }
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

    private async Task EnsureBusinessPartnerCreditLimitAsync(Document document, decimal paymentAmount)
    {
        if (document.Type is not (DocumentType.SaleInvoice or DocumentType.PurchaseInvoice))
        {
            return;
        }

        var outstandingAmount = Math.Max(0m, document.GrandTotal - paymentAmount);
        if (outstandingAmount <= 0m)
        {
            return;
        }

        var businessPartner = await _businessPartnerRepository.GetAsync(document.BusinessPartnerId);
        if (businessPartner.CreditLimit <= 0m)
        {
            return;
        }

        // Customers carry receivables as a positive balance; supplier debt is
        // represented as a negative balance. Both are checked against the
        // same user-defined exposure limit before stock or money is posted.
        var projectedExposure = document.Type == DocumentType.SaleInvoice
            ? Math.Max(0m, businessPartner.Balance) + outstandingAmount
            : Math.Max(0m, -businessPartner.Balance) + outstandingAmount;

        if (projectedExposure > businessPartner.CreditLimit)
        {
            throw new BusinessException("NoxStock:CreditLimitExceeded")
                .WithData("PartnerName", businessPartner.Name)
                .WithData("CreditLimit", businessPartner.CreditLimit)
                .WithData("ProjectedExposure", projectedExposure);
        }
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
        string? description = null,
        string? operationKey = null)
    {
        if (paymentAmount <= 0)
        {
            return;
        }

        if (!accountId.HasValue)
        {
            throw new BusinessException("NoxStock:PaymentRequiresAccount");
        }

        var account = await _accountRepository.GetAsync(accountId.Value);
        if (!string.Equals(account.Currency, document.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("NoxStock:PaymentAccountCurrencyDoesNotMatchDocument")
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
                description ?? $"{document.DocNumber} ödeme hareketi",
                operationKey
            )
        );
    }

    private static string? NormalizeOperationKey(string? operationKey)
    {
        if (string.IsNullOrWhiteSpace(operationKey))
        {
            return null;
        }

        var normalized = operationKey.Trim();
        if (normalized.Length > 100)
        {
            throw new BusinessException("NoxStock:OperationKeyTooLong");
        }

        return normalized;
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
