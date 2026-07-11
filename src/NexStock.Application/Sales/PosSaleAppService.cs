using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NexStock.Accounts;
using NexStock.BusinessPartners;
using NexStock.Documents;
using NexStock.Permissions;
using NexStock.Products;
using NexStock.Settings;
using NexStock.StockMovements;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;

namespace NexStock.Sales;

[Authorize(NexStockPermissions.Sales.Default)]
public class PosSaleAppService : ApplicationService, IPosSaleAppService
{
    private const string WalkInCustomerName = "POS Perakende Müşterisi";

    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<AccountTransaction, Guid> _accountTransactionRepository;
    private readonly IRepository<StockMovement, Guid> _stockMovementRepository;
    private readonly ISettingProvider _settingProvider;

    public PosSaleAppService(
        IRepository<Document, Guid> documentRepository,
        IRepository<Product, Guid> productRepository,
        IRepository<BusinessPartner, Guid> businessPartnerRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<AccountTransaction, Guid> accountTransactionRepository,
        IRepository<StockMovement, Guid> stockMovementRepository,
        ISettingProvider settingProvider)
    {
        _documentRepository = documentRepository;
        _productRepository = productRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _accountRepository = accountRepository;
        _accountTransactionRepository = accountTransactionRepository;
        _stockMovementRepository = stockMovementRepository;
        _settingProvider = settingProvider;
    }

    public async Task<List<PosProductLookupDto>> GetProductsAsync(PosProductLookupInputDto input)
    {
        var query = await _productRepository.GetQueryableAsync();
        var filter = input.Filter?.Trim();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var normalizedFilter = filter.ToLower();
            query = query.Where(x =>
                x.Name.ToLower().Contains(normalizedFilter) ||
                x.Sku.ToLower().Contains(normalizedFilter) ||
                (!string.IsNullOrEmpty(x.Barcode) && x.Barcode.Contains(normalizedFilter)));
        }

        var maxResultCount = Math.Clamp(input.MaxResultCount, 1, 50);
        var products = query
            .OrderBy(x => x.Name)
            .Take(maxResultCount)
            .Select(x => new PosProductLookupDto
            {
                Id = x.Id,
                Name = x.Name,
                Sku = x.Sku,
                Barcode = x.Barcode,
                Unit = x.Unit,
                SalePrice = x.SalePrice,
                Currency = string.IsNullOrWhiteSpace(x.SaleCurrency) ? "TRY" : x.SaleCurrency,
                Quantity = x.Quantity,
                TrackStock = x.TrackStock
            });

        return await AsyncExecuter.ToListAsync(products);
    }

    public async Task<PosCheckoutOptionsDto> GetCheckoutOptionsAsync()
    {
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
            DefaultCashAccountId = ParseGuid(await _settingProvider.GetOrNullAsync(NexStockSettings.DefaultCashAccountId)),
            DefaultBankAccountId = ParseGuid(await _settingProvider.GetOrNullAsync(NexStockSettings.DefaultBankAccountId))
        };
    }

    [Authorize(NexStockPermissions.Sales.Create)]
    public async Task<PosSaleDto> CreateAsync(CreatePosSaleDto input)
    {
        if (input.Items.Count == 0)
        {
            throw new BusinessException("NexStock:PosSaleRequiresItems");
        }

        if (input.PaymentMethod is not (PosPaymentMethod.Cash or PosPaymentMethod.Bank or PosPaymentMethod.CustomerCredit))
        {
            throw new BusinessException("NexStock:InvalidPosPaymentMethod");
        }

        var cartItems = input.Items
            .GroupBy(x => x.ProductId)
            .Select(group => new CreatePosSaleItemDto
            {
                ProductId = group.Key,
                Quantity = group.Sum(x => x.Quantity)
            })
            .ToList();

        if (cartItems.Any(x => x.Quantity <= 0))
        {
            throw new BusinessException("NexStock:PosSaleQuantityMustBePositive");
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
            Description = input.Note
        };

        foreach (var cartItem in cartItems)
        {
            var product = await _productRepository.GetAsync(cartItem.ProductId);
            var productCurrency = string.IsNullOrWhiteSpace(product.SaleCurrency) ? "TRY" : product.SaleCurrency;
            if (document.Items.Count == 0)
            {
                document.Currency = productCurrency;
            }
            else if (!string.Equals(document.Currency, productCurrency, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("NexStock:PosSaleRequiresSingleCurrency");
            }

            if (product.TrackStock && product.Quantity < cartItem.Quantity)
            {
                throw new BusinessException("NexStock:InsufficientStock")
                    .WithData("ProductName", product.Name)
                    .WithData("AvailableQuantity", product.Quantity);
            }

            document.Items.Add(new DocumentItem(GuidGenerator.Create(), CurrentTenant.Id)
            {
                ProductId = product.Id,
                Quantity = cartItem.Quantity,
                UnitPrice = product.SalePrice,
                VatRate = 0,
                DiscountRate = 0,
                Total = product.SalePrice * cartItem.Quantity
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
            document.Status = DocumentStatus.Paid;
        }
        else
        {
            if (businessPartner.CreditLimit > 0 && businessPartner.Balance + document.GrandTotal > businessPartner.CreditLimit)
            {
                throw new BusinessException("NexStock:CreditLimitExceeded")
                    .WithData("CreditLimit", businessPartner.CreditLimit);
            }

            businessPartner.Balance += document.GrandTotal;
            await _businessPartnerRepository.UpdateAsync(businessPartner);
            document.Status = DocumentStatus.Approved;
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

    [Authorize(NexStockPermissions.Sales.Cancel)]
    public async Task<PosSaleDto> CancelAsync(Guid id)
    {
        var queryable = await _documentRepository.WithDetailsAsync(x => x.Items);
        var document = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Id == id));
        if (document is null || document.Type != DocumentType.PosSale)
        {
            throw new BusinessException("NexStock:PosSaleNotFound");
        }

        if (document.Status is not (DocumentStatus.Approved or DocumentStatus.Paid))
        {
            throw new BusinessException("NexStock:PosSaleNotCancellable")
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

        document.Status = DocumentStatus.Cancelled;
        await _documentRepository.UpdateAsync(document, autoSave: true);

        return await MapSaleAsync(document, businessPartner.Name, document.PaidAmount > 0
            ? PosPaymentMethod.Cash
            : PosPaymentMethod.CustomerCredit);
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
                throw new BusinessException("NexStock:PosSaleRequiresCustomer");
            }

            if (paymentMethod == PosPaymentMethod.CustomerCredit &&
                !string.Equals(businessPartner.Currency, currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("NexStock:PosSaleCustomerCurrencyMismatch");
            }

            return businessPartner;
        }

        if (paymentMethod == PosPaymentMethod.CustomerCredit)
        {
            throw new BusinessException("NexStock:PosSaleRequiresCustomer");
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
            throw new BusinessException("NexStock:PosSaleRequiresAccount");
        }

        var account = await _accountRepository.GetAsync(accountId.Value);
        var isValidAccountType = paymentMethod == PosPaymentMethod.Cash
            ? account.Type == AccountType.Cash
            : account.Type is AccountType.Bank or AccountType.CreditCard;
        if (!account.IsActive || !isValidAccountType)
        {
            throw new BusinessException("NexStock:InvalidPosSaleAccount");
        }

        if (!string.Equals(account.Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("NexStock:PosSaleAccountCurrencyMismatch");
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
        return await _settingProvider.GetOrNullAsync(NexStockSettings.PosSaleNumberPrefix) ?? "POS";
    }

    private static Guid? ParseGuid(string? value)
    {
        return Guid.TryParse(value, out var result) ? result : null;
    }
}
