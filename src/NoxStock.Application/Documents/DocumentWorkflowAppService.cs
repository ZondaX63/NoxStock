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
using NoxStock.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.Settings;

namespace NoxStock.Documents;

public class DocumentWorkflowAppService : ApplicationService, IDocumentWorkflowAppService
{
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<DocumentItem, Guid> _documentItemRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<AccountTransaction, Guid> _accountTransactionRepository;
    private readonly IRepository<StockMovement, Guid> _stockMovementRepository;
    private readonly IProductCostProfileService _productCostProfileService;
    private readonly IProductPriceCalculator _productPriceCalculator;
    private readonly IRepository<ExchangeRate, Guid> _exchangeRateRepository;
    private readonly IRepository<DocumentExchangeRateSnapshot, Guid> _exchangeRateSnapshotRepository;
    private readonly CommercialCommandCoordinator _commandCoordinator;
    private readonly ISettingProvider _settingProvider;
    private readonly PaymentAllocationManager _paymentAllocationManager;

    public DocumentWorkflowAppService(
        IRepository<Document, Guid> documentRepository,
        IRepository<DocumentItem, Guid> documentItemRepository,
        IRepository<Product, Guid> productRepository,
        IRepository<BusinessPartner, Guid> businessPartnerRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<AccountTransaction, Guid> accountTransactionRepository,
        IRepository<StockMovement, Guid> stockMovementRepository,
        IProductCostProfileService productCostProfileService,
        IProductPriceCalculator productPriceCalculator,
        IRepository<ExchangeRate, Guid> exchangeRateRepository,
        IRepository<DocumentExchangeRateSnapshot, Guid> exchangeRateSnapshotRepository,
        CommercialCommandCoordinator commandCoordinator,
        ISettingProvider settingProvider,
        PaymentAllocationManager paymentAllocationManager)
    {
        _documentRepository = documentRepository;
        _documentItemRepository = documentItemRepository;
        _productRepository = productRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _accountRepository = accountRepository;
        _accountTransactionRepository = accountTransactionRepository;
        _stockMovementRepository = stockMovementRepository;
        _productCostProfileService = productCostProfileService;
        _productPriceCalculator = productPriceCalculator;
        _exchangeRateRepository = exchangeRateRepository;
        _exchangeRateSnapshotRepository = exchangeRateSnapshotRepository;
        _commandCoordinator = commandCoordinator;
        _settingProvider = settingProvider;
        _paymentAllocationManager = paymentAllocationManager;
    }

    [Authorize(NoxStockPermissions.Documents.Create)]
    [UnitOfWork(true)]
    [NoxStock.Licensing.TenantLicenseWrite]
    public async Task<DocumentDto> CreateReturnAsync(CreateReturnDocumentInputDto input)
    {
        CurrentTenant.RequireTenantId();
        var source = await GetDocumentWithItemsAsync(input.SourceDocumentId);
        if (source.Status is not (DocumentStatus.Approved or DocumentStatus.PartiallyPaid or DocumentStatus.Paid) ||
            source.Type is not (DocumentType.SaleInvoice or DocumentType.PurchaseInvoice or DocumentType.PosSale))
            throw new BusinessException("NoxStock:ReturnSourceNotEligible");
        if (input.Items.Count == 0 || input.Items.GroupBy(x => x.SourceDocumentItemId).Any(x => x.Count() > 1))
            throw new BusinessException("NoxStock:InvalidReturnLines");

        var returnType = source.Type == DocumentType.PurchaseInvoice ? DocumentType.PurchaseReturn : DocumentType.SalesReturn;
        var priorReturns = await _documentRepository.GetListAsync(x => x.SourceDocumentId == source.Id &&
            x.Status != DocumentStatus.Cancelled && x.Type == returnType);
        var priorReturnIds = priorReturns.Select(x => x.Id).ToArray();
        var priorItems = priorReturnIds.Length == 0 ? [] : await GetItemsForDocumentsAsync(priorReturnIds);
        var sourceItems = source.Items.ToDictionary(x => x.Id);
        var result = new Document(GuidGenerator.Create(), CurrentTenant.Id)
        {
            SourceDocumentId = source.Id, BusinessPartnerId = source.BusinessPartnerId,
            DocNumber = $"{source.DocNumber}-I-{GuidGenerator.Create().ToString("N")[..8]}",
            Type = returnType, Status = DocumentStatus.Draft, Currency = source.Currency,
            ExchangeRate = source.ExchangeRate, Date = input.Date == default ? Clock.Now : input.Date,
            Description = input.Description ?? $"{source.DocNumber} kaynaklı iade"
        };
        result.CopyPrintSnapshotFrom(source);
        foreach (var line in input.Items)
        {
            if (!sourceItems.TryGetValue(line.SourceDocumentItemId, out var sourceItem) || line.Quantity <= 0m)
                throw new BusinessException("NoxStock:InvalidReturnLines");
            var previouslyReturned = priorItems.Where(x => x.SourceDocumentItemId == sourceItem.Id).Sum(x => x.Quantity);
            if (line.Quantity + previouslyReturned > sourceItem.Quantity)
                throw new BusinessException("NoxStock:ReturnQuantityExceedsSource");
            var item = new DocumentItem(GuidGenerator.Create(), CurrentTenant.Id)
            {
                SourceDocumentItemId = sourceItem.Id, ProductId = sourceItem.ProductId, Quantity = line.Quantity,
                UnitPrice = sourceItem.UnitPrice, VatRate = sourceItem.VatRate, DiscountRate = sourceItem.DiscountRate,
                PurchaseDiscount1 = sourceItem.PurchaseDiscount1, PurchaseDiscount2 = sourceItem.PurchaseDiscount2,
                PurchaseDiscount3 = sourceItem.PurchaseDiscount3, PurchaseDiscount4 = sourceItem.PurchaseDiscount4,
                SalesDiscount1 = sourceItem.SalesDiscount1, SalesDiscount2 = sourceItem.SalesDiscount2,
                SalesDiscount3 = sourceItem.SalesDiscount3, SalesDiscount4 = sourceItem.SalesDiscount4,
                Total = Math.Round(sourceItem.Total / sourceItem.Quantity * line.Quantity, 2),
                PriceCurrencyCode = sourceItem.PriceCurrencyCode, PriceExchangeRate = sourceItem.PriceExchangeRate,
                PriceRateDate = sourceItem.PriceRateDate, UnitPriceTry = sourceItem.UnitPriceTry,
                ProductCostHistoryId = sourceItem.ProductCostHistoryId, CostCurrencyCode = sourceItem.CostCurrencyCode,
                CostCurrencyAmount = sourceItem.CostCurrencyAmount, CostTryAmount = sourceItem.CostTryAmount,
                TotalCostTryAmount = sourceItem.CostTryAmount * line.Quantity
            };
            item.CopyProductSnapshotFrom(sourceItem);
            result.Items.Add(item);
        }
        result.Subtotal = result.Items.Sum(x => x.Quantity * x.UnitPrice);
        result.DiscountTotal = result.Items.Sum(x => x.Quantity * x.UnitPrice * x.DiscountRate / 100m);
        result.VatTotal = result.Items.Sum(x => x.Total) - result.Subtotal + result.DiscountTotal;
        result.GrandTotal = result.Items.Sum(x => x.Total);
        await _documentRepository.InsertAsync(result, autoSave: true);
        return await MapDocumentAsync(result);
    }

    [Authorize(NoxStockPermissions.Documents.Approve)]
    [UnitOfWork(true)]
    [NoxStock.Licensing.TenantLicenseWrite]
    public async Task<DocumentDto> ApproveAsync(Guid id, ApproveDocumentInputDto input)
    {
        try
        {
            CurrentTenant.RequireTenantId();
            var operationKey = NormalizeOperationKey(input.OperationKey);
            return await _commandCoordinator.ExecuteAsync(
                CurrentTenant.Id,
                CommercialCommandType.DocumentApproval,
                operationKey,
                id,
                () => ApproveInternalAsync(id, input),
                async documentId => await MapDocumentAsync(await GetDocumentWithItemsAsync(documentId)));
        }
        catch (Exception exception) when (IsConcurrencyException(exception))
        {
            // Product and document aggregates use ABP's ConcurrencyStamp. A stale
            // stock read must be presented as a retryable commercial conflict,
            // rather than leaking a persistence-provider exception to the caller.
            throw new BusinessException("NoxStock:StockChanged");
        }
    }

    private async Task<DocumentDto> ApproveInternalAsync(Guid id, ApproveDocumentInputDto input)
    {
        CurrentTenant.RequireTenantId();
        var document = await GetDocumentWithItemsAsync(id);

        if (document.Status is not (DocumentStatus.Draft or DocumentStatus.Sent))
        {
            throw new BusinessException("NoxStock:DocumentStatusNotApprovable")
                .WithData("Status", document.Status);
        }

        if (document.Type is not (DocumentType.SaleInvoice or DocumentType.PurchaseInvoice or DocumentType.Collection or DocumentType.Payment or DocumentType.SalesReturn or DocumentType.PurchaseReturn))
        {
            throw new BusinessException("NoxStock:DocumentTypeNotApprovable")
                .WithData("Type", document.Type);
        }

        if (document.Items.Count == 0 && document.Type is not (DocumentType.Collection or DocumentType.Payment))
        {
            throw new BusinessException("NoxStock:DocumentRequiresItems");
        }

        if (document.Type is DocumentType.SaleInvoice or DocumentType.PurchaseReturn)
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

        if (document.Type == DocumentType.SaleInvoice)
        {
            await EnsureSufficientStockAsync(document);
        }

        if (document.Type is DocumentType.SaleInvoice or DocumentType.PurchaseInvoice)
        {
            await CaptureInvoicePrintSnapshotAsync(document);
        }

        if (document.Type is DocumentType.SaleInvoice or DocumentType.PurchaseInvoice or DocumentType.SalesReturn or DocumentType.PurchaseReturn)
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
        document.PaidAmount = 0m;
        
        var targetStatus = document.Type is DocumentType.Collection or DocumentType.Payment
            ? DocumentStatus.Completed
            : paymentAmount >= document.GrandTotal && paymentAmount > 0
                ? DocumentStatus.Paid
                : paymentAmount > 0
                    ? DocumentStatus.PartiallyPaid
                    : DocumentStatus.Approved;
        document.TransitionTo(targetStatus);

        var paymentTransaction = await ApplyPaymentAsync(document, input.AccountId, paymentAmount, document.Date);
        if (paymentTransaction is not null && document.Type is DocumentType.SaleInvoice or DocumentType.PurchaseInvoice)
        {
            await _paymentAllocationManager.CreateAsync(paymentTransaction, document, paymentAmount, document.Date);
        }

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
        var paymentTransaction = await ApplyPaymentAsync(
            document, input.AccountId, input.Amount, input.PaymentDate, input.Description, operationKey);
        await _paymentAllocationManager.CreateAsync(paymentTransaction!, document, input.Amount, input.PaymentDate);

        await _documentRepository.UpdateAsync(document, autoSave: true);
        return await MapDocumentAsync(document);
    }

    [Authorize(NoxStockPermissions.Documents.Cancel)]
    [UnitOfWork(true)]
    [NoxStock.Licensing.TenantLicenseWrite]
    public async Task<DocumentDto> CancelAsync(Guid id, CancelDocumentInputDto? input = null)
    {
        CurrentTenant.RequireTenantId();
        var operationKey = NormalizeOperationKey(input?.OperationKey);
        return await _commandCoordinator.ExecuteAsync(
            CurrentTenant.Id,
            CommercialCommandType.DocumentCancellation,
            operationKey,
            id,
            () => CancelInternalAsync(id),
            async documentId => await MapDocumentAsync(await GetDocumentWithItemsAsync(documentId)));
    }

    private async Task<DocumentDto> CancelInternalAsync(Guid id)
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

    private Task<List<DocumentItem>> GetItemsForDocumentsAsync(Guid[] documentIds) =>
        _documentItemRepository.GetListAsync(x => documentIds.Contains(x.DocumentId));

    private async Task ApplyStockAsync(Document document)
    {
        foreach (var item in document.Items)
        {
            var product = await _productRepository.GetAsync(item.ProductId);
            if (!product.TrackStock)
            {
                continue;
            }

            var quantityDelta = document.Type switch
            {
                DocumentType.SaleInvoice => -item.Quantity,
                DocumentType.SalesReturn => item.Quantity,
                DocumentType.PurchaseReturn => -item.Quantity,
                _ => item.Quantity
            };

            product.Quantity += quantityDelta;
            await _productRepository.UpdateAsync(product);

            await _stockMovementRepository.InsertAsync(
                new StockMovement(
                    GuidGenerator.Create(),
                    CurrentTenant.Id,
                    product.Id,
                    document.Type == DocumentType.SaleInvoice
                        ? StockMovementType.SalesInvoice
                        : document.Type == DocumentType.SalesReturn || document.Type == DocumentType.PurchaseReturn
                            ? StockMovementType.Return
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

    private async Task CaptureInvoicePrintSnapshotAsync(Document document)
    {
        var businessPartner = await _businessPartnerRepository.GetAsync(document.BusinessPartnerId);
        var productIds = document.Items.Select(item => item.ProductId).Distinct().ToList();
        var products = await _productRepository.GetListAsync(product => productIds.Contains(product.Id));
        var productsById = products.ToDictionary(product => product.Id);

        if (productsById.Count != productIds.Count)
        {
            throw new BusinessException("NoxStock:InvalidProductReference");
        }

        document.CapturePrintSnapshot(
            Clock.Now,
            await GetSettingAsync(NoxStockSettings.CompanyName),
            await GetSettingAsync(NoxStockSettings.Address),
            await GetSettingAsync(NoxStockSettings.Phone),
            await GetSettingAsync(NoxStockSettings.Email),
            await GetSettingAsync(NoxStockSettings.TaxOffice),
            await GetSettingAsync(NoxStockSettings.TaxNumber),
            businessPartner.Name,
            businessPartner.Address,
            businessPartner.TaxOffice,
            businessPartner.TaxNumber);

        foreach (var item in document.Items)
        {
            var product = productsById[item.ProductId];
            item.CaptureProductSnapshot(product.Name, product.Sku, product.Unit);
        }
    }

    private async Task<string> GetSettingAsync(string name)
    {
        return await _settingProvider.GetOrNullAsync(name) ?? string.Empty;
    }

    private async Task EnsureSufficientStockAsync(Document document)
    {
        var requestedQuantities = document.Items
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        var productIds = requestedQuantities.Keys.ToList();
        var products = await _productRepository.GetListAsync(product => productIds.Contains(product.Id));
        var productsById = products.ToDictionary(product => product.Id);

        var insufficientProducts = requestedQuantities
            .Where(request => productsById.TryGetValue(request.Key, out var product) &&
                              product.TrackStock &&
                              product.Quantity < request.Value)
            .Select(request =>
            {
                var product = productsById[request.Key];
                return $"{product.Id} ({product.Sku}): mevcut {product.Quantity}, istenen {request.Value}";
            })
            .ToList();

        if (insufficientProducts.Count > 0)
        {
            throw new BusinessException("NoxStock:InsufficientStock")
                .WithData("InsufficientProducts", string.Join("; ", insufficientProducts));
        }
    }

    private static bool IsConcurrencyException(Exception exception)
    {
        // EF Core's provider exception is converted to ABP's concurrency exception
        // below the application layer. Keep the application layer independent from
        // EF Core while translating that known, retryable condition for callers.
        return exception.GetType().Name is "AbpDbConcurrencyException" or "DbUpdateConcurrencyException";
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
            // The draft may carry a manually entered TRY price for a product
            // whose automatic price cannot yet be calculated.
            if (usesDynamicPricing && !calculation.IsAvailable && !item.IsUnitPriceOverridden)
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
        else if (document.Type == DocumentType.SalesReturn)
        {
            businessPartner.Balance -= document.GrandTotal;
        }
        else if (document.Type == DocumentType.PurchaseReturn)
        {
            businessPartner.Balance += document.GrandTotal;
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
        else if (document.Type == DocumentType.SalesReturn)
        {
            businessPartner.Balance += document.GrandTotal;
        }
        else if (document.Type == DocumentType.PurchaseReturn)
        {
            businessPartner.Balance -= document.GrandTotal;
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

    private async Task<AccountTransaction?> ApplyPaymentAsync(
        Document document,
        Guid? accountId,
        decimal paymentAmount,
        DateTime transactionDate,
        string? description = null,
        string? operationKey = null)
    {
        if (paymentAmount <= 0)
        {
            return null;
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

        var transaction = new AccountTransaction(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                account.Id,
                transactionType,
                amountDelta,
                document.Currency,
                transactionDate,
                document.Id,
                description ?? $"{document.DocNumber} ödeme hareketi",
                operationKey);
        transaction.BusinessPartnerId = document.BusinessPartnerId;
        // The allocation workflow locks the persisted source row inside this same UoW.
        // Saving here does not commit the surrounding commercial transaction.
        await _accountTransactionRepository.InsertAsync(transaction, autoSave: true);
        return transaction;
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
            await _paymentAllocationManager.ReverseForTransactionAsync(transaction.Id, CurrentUser.Id);
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
