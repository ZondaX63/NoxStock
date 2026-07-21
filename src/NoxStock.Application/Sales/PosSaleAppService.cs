using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.Accounts;
using NoxStock.BusinessPartners;
using NoxStock.Documents;
using NoxStock.Permissions;
using NoxStock.Products;
using NoxStock.Settings;
using NoxStock.StockMovements;
using NoxStock.MultiTenancy;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Volo.Abp.Uow;

namespace NoxStock.Sales;

[Authorize(NoxStockPermissions.Sales.Default)]
public class PosSaleAppService : ApplicationService, IPosSaleAppService
{
    private const string WalkInCustomerName = "POS Perakende Müşterisi";

    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<ProductOEMCode, Guid> _productOemCodeRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<AccountTransaction, Guid> _accountTransactionRepository;
    private readonly IRepository<StockMovement, Guid> _stockMovementRepository;
    private readonly IRepository<ProductCostHistory, Guid> _costHistoryRepository;
    private readonly ISettingProvider _settingProvider;
    private readonly IProductPriceCalculator _priceCalculator;

    public PosSaleAppService(
        IRepository<Document, Guid> documentRepository,
        IRepository<Product, Guid> productRepository,
        IRepository<ProductOEMCode, Guid> productOemCodeRepository,
        IRepository<BusinessPartner, Guid> businessPartnerRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<AccountTransaction, Guid> accountTransactionRepository,
        IRepository<StockMovement, Guid> stockMovementRepository,
        IRepository<ProductCostHistory, Guid> costHistoryRepository,
        ISettingProvider settingProvider,
        IProductPriceCalculator priceCalculator)
    {
        _documentRepository = documentRepository;
        _productRepository = productRepository;
        _productOemCodeRepository = productOemCodeRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _accountRepository = accountRepository;
        _accountTransactionRepository = accountTransactionRepository;
        _stockMovementRepository = stockMovementRepository;
        _costHistoryRepository = costHistoryRepository;
        _settingProvider = settingProvider;
        _priceCalculator = priceCalculator;
    }

    public async Task<List<PosProductLookupDto>> GetProductsAsync(PosProductLookupInputDto input)
    {
        CurrentTenant.RequireTenantId();
        var targetCurrency = NormalizePosCurrency(input.Currency);
        var query = await _productRepository.GetQueryableAsync();
        var filter = input.Filter?.Trim();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var normalizedFilter = filter.ToLower();
            var normalizedOemFilter = ProductOemCodeNormalizer.Normalize(filter);
            query = query.Where(x =>
                x.Name.ToLower().Contains(normalizedFilter) ||
                x.Sku.ToLower().Contains(normalizedFilter) ||
                (!string.IsNullOrEmpty(x.Barcode) && x.Barcode.Contains(normalizedFilter)) ||
                (normalizedOemFilter.Length > 0 && x.OemCodes.Any(o => o.SearchCode.Contains(normalizedOemFilter))));
        }

        var maxResultCount = Math.Clamp(input.MaxResultCount, 1, 50);
        var products = await AsyncExecuter.ToListAsync(query
            .OrderBy(x => x.Name)
            .Take(maxResultCount));

        var productIds = products.Select(product => product.Id).ToList();
        var oemCodeQuery = await _productOemCodeRepository.GetQueryableAsync();
        var oemCodesByProduct = (await AsyncExecuter.ToListAsync(oemCodeQuery
                .Where(code => productIds.Contains(code.ProductId))))
            .GroupBy(code => code.ProductId)
            .ToDictionary(group => group.Key, group => string.Join(", ", group.Select(code => code.OemCode)));

        var result = new List<PosProductLookupDto>();
        foreach (var product in products)
        {
            var currency = targetCurrency ?? (string.IsNullOrWhiteSpace(product.SaleCurrency) ? "TRY" : product.SaleCurrency);
            var calculation = await _priceCalculator.CalculateAsync(product, currency);
            var costHistory = product.LastPurchaseCostHistoryId.HasValue
                ? await _costHistoryRepository.FindAsync(product.LastPurchaseCostHistoryId.Value)
                : null;
            result.Add(new PosProductLookupDto
            {
                Id = product.Id,
                Name = product.Name,
                Sku = product.Sku,
                Barcode = product.Barcode,
                OemCodes = oemCodesByProduct.GetValueOrDefault(product.Id),
                Unit = product.Unit,
                SalePrice = calculation.UnitPrice,
                IsPriceAvailable = calculation.IsAvailable,
                PriceUnavailableReason = calculation.UnavailableReason,
                Currency = calculation.Currency,
                OriginalSalePrice = product.SalePrice,
                SaleCurrency = product.SaleCurrency,
                PurchasePrice = product.PurchasePrice,
                PurchaseCurrency = product.PurchaseCurrency,
                UsesCurrentExchangeRate = calculation.UsesCurrentExchangeRate,
                ProfitMarkupPercent = product.ProfitMarkupPercent,
                ExchangeRate = calculation.ExchangeRate,
                LastPurchaseCostCurrencyCode = product.LastPurchaseCostCurrencyCode,
                LastPurchaseCostCurrencyAmount = product.LastPurchaseCostCurrencyAmount,
                LastPurchaseCostTryAmount = product.LastPurchaseCostTryAmount,
                LastPurchaseSourceCurrencyCode = costHistory?.SourceCurrencyCode,
                LastPurchaseSourceNetUnitCost = costHistory?.SourceNetUnitCost,
                LastPurchaseSourceExchangeRate = costHistory?.SourceExchangeRate,
                PricingCurrencyCode = product.PricingCurrencyCode,
                PricingCurrencyExchangeRate = calculation.PricingCurrencyExchangeRate,
                PricingCurrencyRateDate = calculation.PricingCurrencyRateDate,
                ReferenceSaleUnitPrice = calculation.ReferenceSaleUnitPrice,
                ProductCostHistoryId = calculation.ProductCostHistoryId,
                Quantity = product.Quantity,
                TrackStock = product.TrackStock
            });
        }

        return result;
    }

    public async Task<PosCheckoutOptionsDto> GetCheckoutOptionsAsync()
    {
        CurrentTenant.RequireTenantId();
        var accounts = await _accountRepository.GetQueryableAsync();
        var businessPartners = await _businessPartnerRepository.GetQueryableAsync();

        return new PosCheckoutOptionsDto
        {
            Accounts = await AsyncExecuter.ToListAsync(accounts
                .Where(x => x.IsActive &&
                    (x.Type == AccountType.Cash || x.Type == AccountType.Bank || x.Type == AccountType.CreditCard))
                .OrderBy(x => x.Name)
                .Select(x => new PosAccountLookupDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Currency = x.Currency,
                    Type = x.Type
                })),
            Customers = await AsyncExecuter.ToListAsync(businessPartners
                .Where(x => x.Type == BusinessPartnerType.Customer || x.Type == BusinessPartnerType.Both)
                .OrderBy(x => x.Name)
                .Select(x => new PosCustomerLookupDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Balance = x.Balance,
                    CreditLimit = x.CreditLimit,
                    Currency = x.Currency
                })),
            DefaultCashAccountId = ParseGuid(await _settingProvider.GetOrNullAsync(NoxStockSettings.DefaultCashAccountId)),
            DefaultBankAccountId = ParseGuid(await _settingProvider.GetOrNullAsync(NoxStockSettings.DefaultBankAccountId))
        };
    }

    [Authorize(NoxStockPermissions.Sales.Create)]
    [UnitOfWork(true)]
    public async Task<PosSaleDto> CreateAsync(CreatePosSaleDto input)
    {
        CurrentTenant.RequireTenantId();
        var targetCurrency = NormalizePosCurrency(input.Currency);
        var operationKey = NormalizeOperationKey(input.OperationKey);
        if (operationKey is not null)
        {
            var existingQuery = await _documentRepository.WithDetailsAsync(x => x.Items);
            var existingSale = await AsyncExecuter.FirstOrDefaultAsync(existingQuery.Where(x =>
                x.Type == DocumentType.PosSale && x.OperationKey == operationKey));
            if (existingSale is not null)
            {
                var existingPartner = await _businessPartnerRepository.GetAsync(existingSale.BusinessPartnerId);
                return await MapSaleAsync(existingSale, existingPartner.Name, input.PaymentMethod);
            }
        }

        if (input.Items.Count == 0)
        {
            throw new BusinessException("NoxStock:PosSaleRequiresItems");
        }

        if (input.PaymentMethod is not (PosPaymentMethod.Cash or PosPaymentMethod.Bank or PosPaymentMethod.CustomerCredit))
        {
            throw new BusinessException("NoxStock:InvalidPosPaymentMethod");
        }

        var cartItems = input.Items
            .GroupBy(x => x.ProductId)
            .Select(group => new CreatePosSaleItemDto
            {
                ProductId = group.Key,
                Quantity = group.Sum(x => x.Quantity),
                UnitPrice = group.Last().UnitPrice,
                IsUnitPriceOverridden = group.Any(x => x.IsUnitPriceOverridden)
            })
            .ToList();

        if (cartItems.Any(x => x.Quantity <= 0))
        {
            throw new BusinessException("NoxStock:PosSaleQuantityMustBePositive");
        }

        var saleDate = Clock.Now;
        var document = new Document(GuidGenerator.Create(), CurrentTenant.Id)
        {
            DocNumber = $"{await GetPosSalePrefixAsync()}-{saleDate:yyyyMMdd-HHmmss}-{GuidGenerator.Create().ToString("N")[..6].ToUpperInvariant()}",
            Type = DocumentType.PosSale,
            Status = DocumentStatus.Draft,
            Currency = "TRY",
            ExchangeRate = 1,
            Date = saleDate,
            Description = input.Note,
            OperationKey = operationKey
        };

        foreach (var cartItem in cartItems)
        {
            var product = await _productRepository.GetAsync(cartItem.ProductId);
            var productCurrency = targetCurrency ?? (string.IsNullOrWhiteSpace(product.SaleCurrency) ? "TRY" : product.SaleCurrency);
            var calculation = await _priceCalculator.CalculateAsync(product, productCurrency);
            if (!calculation.IsAvailable)
            {
                throw new BusinessException(calculation.UnavailableReason ?? "NoxStock:ProductApprovedPurchaseCostUnavailable")
                    .WithData("ProductName", product.Name);
            }
            var usesManualPrice = cartItem.IsUnitPriceOverridden && cartItem.UnitPrice is > 0;
            var unitPrice = usesManualPrice ? cartItem.UnitPrice!.Value : calculation.UnitPrice;
            if (document.Items.Count == 0)
            {
                document.Currency = calculation.Currency;
                document.ExchangeRate = calculation.ExchangeRate;
            }
            else if (!string.Equals(document.Currency, calculation.Currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("NoxStock:PosSaleRequiresSingleCurrency");
            }

            if (product.TrackStock && product.Quantity < cartItem.Quantity)
            {
                throw new BusinessException("NoxStock:InsufficientStock")
                    .WithData("ProductName", product.Name)
                    .WithData("AvailableQuantity", product.Quantity);
            }

            document.Items.Add(new DocumentItem(GuidGenerator.Create(), CurrentTenant.Id)
            {
                ProductId = product.Id,
                Quantity = cartItem.Quantity,
                UnitPrice = unitPrice,
                VatRate = 0,
                DiscountRate = 0,
                Total = unitPrice * cartItem.Quantity,
                IsUnitPriceOverridden = usesManualPrice,
                PriceCurrencyCode = calculation.Currency,
                PriceExchangeRate = calculation.ExchangeRate,
                PriceRateDate = calculation.PricingCurrencyRateDate ?? calculation.RateDate,
                UnitPriceTry = RoundMoney(unitPrice * calculation.ExchangeRate),
                ProductCostHistoryId = calculation.ProductCostHistoryId ?? product.LastPurchaseCostHistoryId,
                PricingMode = usesManualPrice ? ProductPricingMode.Manual : product.PricingMode,
                CostCurrencyCode = product.LastPurchaseCostCurrencyCode,
                CostCurrencyAmount = product.LastPurchaseCostCurrencyAmount,
                CostTryAmount = product.LastPurchaseCostTryAmount,
                AppliedMarkupPercent = calculation.MarkupPercent,
                ReferenceSaleCurrencyCode = calculation.PricingCurrencyCode,
                ReferenceSaleUnitPrice = calculation.ReferenceSaleUnitPrice,
                ReferenceSaleExchangeRate = calculation.PricingCurrencyExchangeRate,
                PricingSnapshotDate = saleDate
            });
        }

        document.Subtotal = document.Items.Sum(x => x.Total);
        document.GrandTotal = document.Subtotal;

        var businessPartner = await ResolveBusinessPartnerAsync(input.BusinessPartnerId, input.PaymentMethod, document.Currency);
        document.BusinessPartnerId = businessPartner.Id;

        Account? account = null;
        if (input.PaymentMethod is PosPaymentMethod.Cash or PosPaymentMethod.Bank)
        {
            account = await ResolvePaymentAccountAsync(input.AccountId, input.PaymentMethod, document.Currency);
            document.PaidAmount = document.GrandTotal;
            document.TransitionTo(DocumentStatus.Paid);
        }
        else
        {
            if (businessPartner.CreditLimit > 0 && businessPartner.Balance + document.GrandTotal > businessPartner.CreditLimit)
            {
                throw new BusinessException("NoxStock:CreditLimitExceeded")
                    .WithData("CreditLimit", businessPartner.CreditLimit);
            }

            businessPartner.Balance += document.GrandTotal;
            await _businessPartnerRepository.UpdateAsync(businessPartner);
            document.TransitionTo(DocumentStatus.Approved);
        }

        await _documentRepository.InsertAsync(document);

        foreach (var item in document.Items)
        {
            var product = await _productRepository.GetAsync(item.ProductId);
            if (!product.TrackStock)
            {
                continue;
            }

            product.Quantity -= item.Quantity;
            await _productRepository.UpdateAsync(product);
            await _stockMovementRepository.InsertAsync(new StockMovement(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                product.Id,
                StockMovementType.PosSale,
                -item.Quantity,
                product.Quantity,
                saleDate,
                document.Id,
                $"{document.DocNumber} POS satış hareketi"));
        }

        if (account is not null)
        {
            account.Balance += document.GrandTotal;
            await _accountRepository.UpdateAsync(account);
            await _accountTransactionRepository.InsertAsync(new AccountTransaction(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                account.Id,
                AccountTransactionType.Collection,
                document.GrandTotal,
                document.Currency,
                saleDate,
                document.Id,
                $"{document.DocNumber} POS tahsilatı"));
        }

        return await MapSaleAsync(document, businessPartner.Name, input.PaymentMethod);
    }

    public async Task<List<PosSaleDto>> GetRecentAsync()
    {
        CurrentTenant.RequireTenantId();
        var documents = await _documentRepository.GetQueryableAsync();
        var businessPartners = await _businessPartnerRepository.GetQueryableAsync();
        var transactions = await _accountTransactionRepository.GetQueryableAsync();
        var accounts = await _accountRepository.GetQueryableAsync();

        var recentSalesQuery =
            from document in documents
            join businessPartner in businessPartners on document.BusinessPartnerId equals businessPartner.Id
            where document.Type == DocumentType.PosSale
            orderby document.Date descending, document.CreationTime descending
            select new PosSaleDto
            {
                Id = document.Id,
                ReceiptNumber = document.DocNumber,
                BusinessPartnerName = businessPartner.Name,
                GrandTotal = document.GrandTotal,
                Currency = document.Currency,
                Date = document.Date,
                Status = document.Status,
                PaymentMethod = PosPaymentMethod.CustomerCredit
            };

        var sales = await AsyncExecuter.ToListAsync(recentSalesQuery.Take(20));

        var saleIds = sales.Select(x => x.Id).ToList();
        if (saleIds.Count == 0)
        {
            return sales;
        }

        var paymentAccounts = await AsyncExecuter.ToListAsync(
            from transaction in transactions
            join account in accounts on transaction.AccountId equals account.Id
            where transaction.DocumentId.HasValue &&
                  saleIds.Contains(transaction.DocumentId.Value) &&
                  transaction.Type == AccountTransactionType.Collection
            select new { DocumentId = transaction.DocumentId!.Value, account.Type });

        foreach (var sale in sales)
        {
            var paymentAccount = paymentAccounts.FirstOrDefault(x => x.DocumentId == sale.Id);
            if (paymentAccount is not null)
            {
                sale.PaymentMethod = paymentAccount.Type == AccountType.Cash
                    ? PosPaymentMethod.Cash
                    : PosPaymentMethod.Bank;
            }
        }

        return sales.Take(20).ToList();
    }

    [Authorize(NoxStockPermissions.Sales.Cancel)]
    [UnitOfWork(true)]
    public async Task<PosSaleDto> CancelAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        var queryable = await _documentRepository.WithDetailsAsync(x => x.Items);
        var document = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Id == id));
        if (document is null || document.Type != DocumentType.PosSale)
        {
            throw new BusinessException("NoxStock:PosSaleNotFound");
        }

        if (document.Status is not (DocumentStatus.Approved or DocumentStatus.Paid))
        {
            throw new BusinessException("NoxStock:PosSaleNotCancellable")
                .WithData("Status", document.Status);
        }

        var stockMovements = await _stockMovementRepository.GetListAsync(x =>
            x.DocumentId == document.Id && x.Type == StockMovementType.PosSale);
        foreach (var movement in stockMovements)
        {
            var product = await _productRepository.GetAsync(movement.ProductId);
            product.Quantity -= movement.Quantity;
            await _productRepository.UpdateAsync(product);
            await _stockMovementRepository.InsertAsync(new StockMovement(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                product.Id,
                StockMovementType.Reversal,
                -movement.Quantity,
                product.Quantity,
                Clock.Now,
                document.Id,
                $"{document.DocNumber} POS iptal stok ters kaydı"));
        }

        var accountTransactions = await _accountTransactionRepository.GetListAsync(x =>
            x.DocumentId == document.Id && x.Type != AccountTransactionType.Reversal);
        foreach (var transaction in accountTransactions)
        {
            var account = await _accountRepository.GetAsync(transaction.AccountId);
            var reversalAmount = -transaction.Amount;
            account.Balance += reversalAmount;
            await _accountRepository.UpdateAsync(account);
            await _accountTransactionRepository.InsertAsync(new AccountTransaction(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                account.Id,
                AccountTransactionType.Reversal,
                reversalAmount,
                transaction.Currency,
                Clock.Now,
                document.Id,
                $"{document.DocNumber} POS iptal tahsilat ters kaydı"));
        }

        var businessPartner = await _businessPartnerRepository.GetAsync(document.BusinessPartnerId);
        if (document.PaidAmount < document.GrandTotal)
        {
            businessPartner.Balance -= document.GrandTotal - document.PaidAmount;
            await _businessPartnerRepository.UpdateAsync(businessPartner);
        }

        document.TransitionTo(DocumentStatus.Cancelled);
        await _documentRepository.UpdateAsync(document, autoSave: true);

        return await MapSaleAsync(document, businessPartner.Name, document.PaidAmount > 0
            ? PosPaymentMethod.Cash
            : PosPaymentMethod.CustomerCredit);
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

    private static decimal RoundMoney(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private static string? NormalizePosCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return null;
        }

        var normalized = currency.Trim().ToUpperInvariant();
        if (normalized is not ("TRY" or "USD" or "EUR"))
        {
            throw new BusinessException("NoxStock:UnsupportedCurrency").WithData("Currency", currency);
        }

        return normalized;
    }

    private async Task<BusinessPartner> ResolveBusinessPartnerAsync(
        Guid? businessPartnerId,
        PosPaymentMethod paymentMethod,
        string currency)
    {
        if (businessPartnerId.HasValue)
        {
            var businessPartner = await _businessPartnerRepository.GetAsync(businessPartnerId.Value);
            if (businessPartner.Type is not (BusinessPartnerType.Customer or BusinessPartnerType.Both))
            {
                throw new BusinessException("NoxStock:PosSaleRequiresCustomer");
            }

            if (paymentMethod == PosPaymentMethod.CustomerCredit &&
                !string.Equals(businessPartner.Currency, currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("NoxStock:PosSaleCustomerCurrencyMismatch");
            }

            return businessPartner;
        }

        if (paymentMethod == PosPaymentMethod.CustomerCredit)
        {
            throw new BusinessException("NoxStock:PosSaleRequiresCustomer");
        }

        var queryable = await _businessPartnerRepository.GetQueryableAsync();
        var walkInCustomer = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Name == WalkInCustomerName));
        if (walkInCustomer is not null)
        {
            return walkInCustomer;
        }

        walkInCustomer = new BusinessPartner(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            WalkInCustomerName,
            BusinessPartnerType.Customer,
            currency);
        await _businessPartnerRepository.InsertAsync(walkInCustomer);
        return walkInCustomer;
    }

    private async Task<Account> ResolvePaymentAccountAsync(
        Guid? accountId,
        PosPaymentMethod paymentMethod,
        string currency)
    {
        if (!accountId.HasValue)
        {
            throw new BusinessException("NoxStock:PosSaleRequiresAccount");
        }

        var account = await _accountRepository.GetAsync(accountId.Value);
        var isValidAccountType = paymentMethod == PosPaymentMethod.Cash
            ? account.Type == AccountType.Cash
            : account.Type is AccountType.Bank or AccountType.CreditCard;
        if (!account.IsActive || !isValidAccountType)
        {
            throw new BusinessException("NoxStock:InvalidPosSaleAccount");
        }

        if (!string.Equals(account.Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("NoxStock:PosSaleAccountCurrencyMismatch");
        }

        return account;
    }

    private static Task<PosSaleDto> MapSaleAsync(Document document, string businessPartnerName, PosPaymentMethod paymentMethod)
    {
        return Task.FromResult(new PosSaleDto
        {
            Id = document.Id,
            ReceiptNumber = document.DocNumber,
            BusinessPartnerName = businessPartnerName,
            PaymentMethod = paymentMethod,
            GrandTotal = document.GrandTotal,
            Currency = document.Currency,
            Date = document.Date,
            Status = document.Status
        });
    }

    private async Task<string> GetPosSalePrefixAsync()
    {
        return await _settingProvider.GetOrNullAsync(NoxStockSettings.PosSaleNumberPrefix) ?? "POS";
    }

    private static Guid? ParseGuid(string? value)
    {
        return Guid.TryParse(value, out var result) ? result : null;
    }
}
