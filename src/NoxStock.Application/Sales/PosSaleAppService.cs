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
using NoxStock.Common;
using NoxStock.Settings;
using NoxStock.StockMovements;
using NoxStock.MultiTenancy;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
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
    private readonly NoxStock.Licensing.ITenantLicenseAccessChecker _licenseAccessChecker;

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
        IProductPriceCalculator priceCalculator,
        NoxStock.Licensing.ITenantLicenseAccessChecker licenseAccessChecker)
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
        _licenseAccessChecker = licenseAccessChecker;
    }

    public async Task<List<PosProductLookupDto>> GetProductsAsync(PosProductLookupInputDto input)
    {
        CurrentTenant.RequireTenantId();
        await _licenseAccessChecker.EnsureFeatureAsync(
            NoxStock.Licensing.LicenseFeatureNames.Pos);
        var query = await _productRepository.GetQueryableAsync();
        var filter = input.Filter?.Trim();
        var maxResultCount = Math.Clamp(input.MaxResultCount, 1, 50);
        List<Product> products;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var normalizedFilter = SearchText.Normalize(filter);
            var normalizedOemFilter = ProductOemCodeNormalizer.Normalize(filter);
            var exactProducts = await AsyncExecuter.ToListAsync(query
                .Where(x =>
                    x.Sku.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c") == normalizedFilter ||
                    (x.Barcode != null && x.Barcode.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c") == normalizedFilter) ||
                    (normalizedOemFilter.Length > 0 && x.OemCodes.Any(o => o.SearchCode == normalizedOemFilter)))
                .OrderBy(x => x.Name)
                .Take(maxResultCount));

            products = exactProducts.Count > 0
                ? exactProducts
                : await AsyncExecuter.ToListAsync(query.Where(x =>
                x.Name.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(normalizedFilter) ||
                x.Sku.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(normalizedFilter) ||
                (!string.IsNullOrEmpty(x.Barcode) && x.Barcode.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(normalizedFilter)) ||
                (normalizedOemFilter.Length > 0 && x.OemCodes.Any(o => o.SearchCode.Contains(normalizedOemFilter))))
                .OrderBy(x => x.Name)
                .Take(maxResultCount));
        }
        else
        {
            products = await AsyncExecuter.ToListAsync(query
                .OrderBy(x => x.Name)
                .Take(maxResultCount));
        }

        var productIds = products.Select(product => product.Id).ToList();
        var oemCodeQuery = await _productOemCodeRepository.GetQueryableAsync();
        var oemCodesByProduct = (await AsyncExecuter.ToListAsync(oemCodeQuery
                .Where(code => productIds.Contains(code.ProductId))))
            .GroupBy(code => code.ProductId)
            .ToDictionary(group => group.Key, group => string.Join(", ", group.Select(code => code.OemCode)));
        var costHistoryIds = products
            .Where(product => product.LastPurchaseCostHistoryId.HasValue)
            .Select(product => product.LastPurchaseCostHistoryId!.Value)
            .Distinct()
            .ToList();
        var costHistoriesById = costHistoryIds.Count == 0
            ? new Dictionary<Guid, ProductCostHistory>()
            : (await _costHistoryRepository.GetListAsync(history => costHistoryIds.Contains(history.Id)))
                .ToDictionary(history => history.Id);

        var result = new List<PosProductLookupDto>();
        foreach (var product in products)
        {
            var calculation = await _priceCalculator.CalculateAsync(product);
            var costHistory = product.LastPurchaseCostHistoryId.HasValue
                ? costHistoriesById.GetValueOrDefault(product.LastPurchaseCostHistoryId.Value)
                : null;
            result.Add(new PosProductLookupDto
            {
                Id = product.Id,
                Name = product.Name,
                Sku = product.Sku,
                Barcode = product.Barcode,
                OemCodes = oemCodesByProduct.GetValueOrDefault(product.Id),
                Unit = product.Unit,
                // SalePrice remains the editable KDV-hariç list price. The
                // separate final amount is what the cashier sees and collects.
                SalePrice = calculation.ListSalePriceInTry,
                ListSalePrice = calculation.ListSalePriceInTry,
                NetSalePrice = calculation.UnitPrice,
                CustomerFinalUnitPrice = calculation.CustomerFinalPriceInTry,
                VatRate = product.VatRate,
                // Varsayılan satış iskontosu ürün değil, müşteri politikasına aittir.
                SalesDiscount1 = 0,
                SalesDiscount2 = 0,
                SalesDiscount3 = 0,
                SalesDiscount4 = 0,
                IsPriceAvailable = calculation.IsAvailable,
                PriceUnavailableReason = calculation.UnavailableReason,
                Currency = calculation.Currency,
                OriginalSalePrice = product.SalePrice,
                PurchasePrice = product.PurchasePrice,
                PurchaseCurrency = product.PurchaseCurrency,
                UsesCurrentExchangeRate = calculation.UsesCurrentExchangeRate,
                ProfitMarkupPercent = product.ProfitMarkupPercent,
                PricingMode = product.PricingMode,
                ExchangeRate = calculation.ExchangeRate,
                LastPurchaseCostCurrencyCode = product.LastPurchaseCostCurrencyCode,
                LastPurchaseCostCurrencyAmount = product.LastPurchaseCostCurrencyAmount,
                LastPurchaseCostTryAmount = product.LastPurchaseCostTryAmount,
                AverageInventoryCostTry = product.AverageInventoryCostTry,
                LastPurchaseSourceCurrencyCode = costHistory?.SourceCurrencyCode,
                LastPurchaseSourceNetUnitCost = costHistory?.SourceNetUnitCost,
                LastPurchaseSourceExchangeRate = costHistory?.SourceExchangeRate,
                ReferenceCurrencyCode = product.PurchaseCurrency,
                ReferenceCurrencyExchangeRate = calculation.ReferenceCurrencyExchangeRate,
                ReferenceCurrencyRateDate = calculation.ReferenceCurrencyRateDate,
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
        await _licenseAccessChecker.EnsureFeatureAsync(
            NoxStock.Licensing.LicenseFeatureNames.Pos);
        var accounts = await _accountRepository.GetQueryableAsync();

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
            DefaultCashAccountId = ParseGuid(await _settingProvider.GetOrNullAsync(NoxStockSettings.DefaultCashAccountId)),
            DefaultBankAccountId = ParseGuid(await _settingProvider.GetOrNullAsync(NoxStockSettings.DefaultBankAccountId))
        };
    }

    public async Task<PagedResultDto<PosCustomerLookupDto>> GetCustomersAsync(PosCustomerLookupInputDto input)
    {
        CurrentTenant.RequireTenantId();
        await _licenseAccessChecker.EnsureFeatureAsync(
            NoxStock.Licensing.LicenseFeatureNames.Pos);

        var query = await _businessPartnerRepository.GetQueryableAsync();
        var filter = input.Filter?.Trim();
        var normalizedFilter = SearchText.Normalize(filter);
        query = query.Where(partner =>
            (partner.Type == BusinessPartnerType.Customer || partner.Type == BusinessPartnerType.Both) &&
            partner.Currency == "TRY");
        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(partner =>
                partner.Name.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(normalizedFilter!) ||
                (partner.TaxNumber != null && partner.TaxNumber.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(normalizedFilter!)) ||
                (partner.Phone != null && partner.Phone.Contains(filter!)));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var maxResultCount = Math.Clamp(input.MaxResultCount, 1, 30);
        var skipCount = Math.Max(input.SkipCount, 0);
        var customers = await AsyncExecuter.ToListAsync(query
            .OrderBy(partner => partner.Name)
            .Skip(skipCount)
            .Take(maxResultCount)
            .Select(partner => new PosCustomerLookupDto
            {
                Id = partner.Id,
                Name = partner.Name,
                TaxNumber = partner.TaxNumber,
                Phone = partner.Phone,
                Balance = partner.Balance,
                CreditLimit = partner.CreditLimit,
                Currency = partner.Currency,
                DefaultSalesDiscount1 = partner.DefaultSalesDiscount1,
                DefaultSalesDiscount2 = partner.DefaultSalesDiscount2,
                DefaultSalesDiscount3 = partner.DefaultSalesDiscount3,
                DefaultSalesDiscount4 = partner.DefaultSalesDiscount4
            }));

        return new PagedResultDto<PosCustomerLookupDto>(totalCount, customers);
    }

    [Authorize(NoxStockPermissions.Sales.Create)]
    [UnitOfWork(true)]
    [NoxStock.Licensing.TenantLicenseWrite]
    public async Task<PosSaleDto> CreateAsync(CreatePosSaleDto input)
    {
        CurrentTenant.RequireTenantId();
        await _licenseAccessChecker.EnsureFeatureAsync(
            NoxStock.Licensing.LicenseFeatureNames.Pos);
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
                Quantity = RoundMoney(group.Sum(x => x.Quantity)),
                UnitPrice = RoundNullable(group.Last().UnitPrice),
                VatRate = RoundNullable(group.Last().VatRate),
                SalesDiscount1 = RoundNullable(group.Last().SalesDiscount1),
                SalesDiscount2 = RoundNullable(group.Last().SalesDiscount2),
                SalesDiscount3 = RoundNullable(group.Last().SalesDiscount3),
                SalesDiscount4 = RoundNullable(group.Last().SalesDiscount4),
                IsUnitPriceOverridden = group.Any(x => x.IsUnitPriceOverridden)
            })
            .ToList();

        if (cartItems.Any(x => x.Quantity <= 0))
        {
            throw new BusinessException("NoxStock:PosSaleQuantityMustBePositive");
        }

        if (cartItems.Any(x => x.VatRate is < 0m or > 100m))
        {
            throw new BusinessException("NoxStock:InvalidPosVatRate");
        }

        if (cartItems.Any(x => new[] { x.SalesDiscount1, x.SalesDiscount2, x.SalesDiscount3, x.SalesDiscount4 }
            .Any(discount => discount is < 0m or > 100m)))
        {
            throw new BusinessException("NoxStock:InvalidPosDiscountRate");
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
        var businessPartner = await ResolveBusinessPartnerAsync(input.BusinessPartnerId, input.PaymentMethod, document.Currency);
        document.BusinessPartnerId = businessPartner.Id;

        foreach (var cartItem in cartItems)
        {
            var product = await _productRepository.GetAsync(cartItem.ProductId);
            var calculation = await _priceCalculator.CalculateAsync(product);
            var usesManualPrice = cartItem.IsUnitPriceOverridden && cartItem.UnitPrice is > 0;
            // A missing product-card price/cost must not make stocked goods
            // unsellable. The cashier can supply a TRY unit price on the line.
            if (!calculation.IsAvailable && !usesManualPrice)
            {
                throw new BusinessException(calculation.UnavailableReason ?? "NoxStock:ProductApprovedPurchaseCostUnavailable")
                    .WithData("ProductName", product.Name);
            }
            var unitPrice = usesManualPrice ? cartItem.UnitPrice!.Value : calculation.ListSalePriceInTry;
            var vatRate = cartItem.VatRate ?? product.VatRate;
            var salesDiscount1 = cartItem.SalesDiscount1 ?? businessPartner.DefaultSalesDiscount1;
            var salesDiscount2 = cartItem.SalesDiscount2 ?? businessPartner.DefaultSalesDiscount2;
            var salesDiscount3 = cartItem.SalesDiscount3 ?? businessPartner.DefaultSalesDiscount3;
            var salesDiscount4 = cartItem.SalesDiscount4 ?? businessPartner.DefaultSalesDiscount4;
            var effectiveDiscountRate = GetEffectiveSalesDiscountRate(
                salesDiscount1, salesDiscount2, salesDiscount3, salesDiscount4);
            var lineNet = unitPrice * cartItem.Quantity;
            var discount = lineNet * effectiveDiscountRate / 100m;
            var taxable = lineNet - discount;
            var vat = taxable * vatRate / 100m;
            var netUnitSale = cartItem.Quantity > 0m ? taxable / cartItem.Quantity : 0m;
            if (calculation.NetCostInTry > 0m && netUnitSale < calculation.NetCostInTry)
            {
                var canOverride = await AuthorizationService.IsGrantedAsync(NoxStockPermissions.Sales.SellBelowCost);
                if (!input.ConfirmBelowCost || !canOverride)
                {
                    throw new BusinessException("NoxStock:SaleBelowInventoryCost")
                        .WithData("ProductName", product.Name)
                        .WithData("InventoryCost", calculation.NetCostInTry)
                        .WithData("NetSalePrice", RoundMoney(netUnitSale))
                        .WithData("CanOverride", canOverride);
                }
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
                VatRate = vatRate,
                DiscountRate = effectiveDiscountRate,
                SalesDiscount1 = salesDiscount1,
                SalesDiscount2 = salesDiscount2,
                SalesDiscount3 = salesDiscount3,
                SalesDiscount4 = salesDiscount4,
                Total = RoundMoney(taxable + vat),
                IsUnitPriceOverridden = usesManualPrice,
                PriceCurrencyCode = calculation.Currency,
                PriceExchangeRate = calculation.ExchangeRate,
                PriceRateDate = calculation.ReferenceCurrencyRateDate ?? calculation.RateDate,
                UnitPriceTry = RoundMoney(unitPrice),
                ProductCostHistoryId = calculation.ProductCostHistoryId ?? product.LastPurchaseCostHistoryId,
                PricingMode = usesManualPrice ? ProductPricingMode.Manual : product.PricingMode,
                CostCurrencyCode = product.LastPurchaseCostCurrencyCode,
                CostCurrencyAmount = product.LastPurchaseCostCurrencyAmount,
                CostTryAmount = calculation.NetCostInTry > 0m
                    ? calculation.NetCostInTry
                    : product.LastPurchaseCostTryAmount,
                TotalCostTryAmount = RoundMoney((calculation.NetCostInTry > 0m
                    ? calculation.NetCostInTry
                    : product.LastPurchaseCostTryAmount ?? 0m) * cartItem.Quantity),
                AppliedMarkupPercent = calculation.MarkupPercent,
                ReferenceCurrencyCode = calculation.ReferenceCurrencyCode,
                ReferenceUnitPrice = calculation.ReferenceSaleUnitPrice,
                ReferenceExchangeRate = calculation.ReferenceCurrencyExchangeRate,
                PricingSnapshotDate = saleDate
            });
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
                    .WithData("PartnerName", businessPartner.Name)
                    .WithData("CreditLimit", businessPartner.CreditLimit)
                    .WithData("ProjectedExposure", businessPartner.Balance + document.GrandTotal);
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
        await _licenseAccessChecker.EnsureFeatureAsync(
            NoxStock.Licensing.LicenseFeatureNames.Pos);
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

    public async Task<PosReceiptDto> GetReceiptAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        await _licenseAccessChecker.EnsureFeatureAsync(NoxStock.Licensing.LicenseFeatureNames.Pos);

        var documentQuery = await _documentRepository.WithDetailsAsync(x => x.Items);
        var document = await AsyncExecuter.FirstOrDefaultAsync(documentQuery.Where(x =>
            x.Id == id && x.Type == DocumentType.PosSale));
        if (document is null)
        {
            throw new BusinessException("NoxStock:PosSaleNotFound");
        }

        var partner = await _businessPartnerRepository.GetAsync(document.BusinessPartnerId);
        var productIds = document.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = (await _productRepository.GetListAsync(x => productIds.Contains(x.Id)))
            .ToDictionary(x => x.Id);
        var paymentMethod = PosPaymentMethod.CustomerCredit;
        var transaction = await _accountTransactionRepository.FirstOrDefaultAsync(x =>
            x.DocumentId == document.Id && x.Type == AccountTransactionType.Collection);
        if (transaction is not null)
        {
            var account = await _accountRepository.GetAsync(transaction.AccountId);
            paymentMethod = account.Type == AccountType.Cash ? PosPaymentMethod.Cash : PosPaymentMethod.Bank;
        }

        return new PosReceiptDto
        {
            Id = document.Id,
            ReceiptNumber = document.DocNumber,
            BusinessPartnerName = partner.Name,
            PaymentMethod = paymentMethod,
            Date = document.Date,
            Status = document.Status,
            Currency = document.Currency,
            Subtotal = document.Subtotal,
            DiscountTotal = document.DiscountTotal,
            VatTotal = document.VatTotal,
            GrandTotal = document.GrandTotal,
            Note = document.Description,
            CompanyName = await _settingProvider.GetOrNullAsync(NoxStockSettings.CompanyName) ?? string.Empty,
            TaxOffice = await _settingProvider.GetOrNullAsync(NoxStockSettings.TaxOffice) ?? string.Empty,
            TaxNumber = await _settingProvider.GetOrNullAsync(NoxStockSettings.TaxNumber) ?? string.Empty,
            Address = await _settingProvider.GetOrNullAsync(NoxStockSettings.Address) ?? string.Empty,
            Phone = await _settingProvider.GetOrNullAsync(NoxStockSettings.Phone) ?? string.Empty,
            Items = document.Items.Select(item => new PosReceiptItemDto
            {
                ProductName = products.GetValueOrDefault(item.ProductId)?.Name ?? "Ürün",
                ProductSku = products.GetValueOrDefault(item.ProductId)?.Sku ?? string.Empty,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                SalesDiscount1 = item.SalesDiscount1,
                SalesDiscount2 = item.SalesDiscount2,
                SalesDiscount3 = item.SalesDiscount3,
                SalesDiscount4 = item.SalesDiscount4,
                EffectiveDiscountRate = item.DiscountRate,
                VatRate = item.VatRate,
                Total = item.Total
            }).ToList()
        };
    }

    [Authorize(NoxStockPermissions.Sales.Cancel)]
    [UnitOfWork(true)]
    [NoxStock.Licensing.TenantLicenseWrite]
    public async Task<PosSaleDto> CancelAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        await _licenseAccessChecker.EnsureFeatureAsync(
            NoxStock.Licensing.LicenseFeatureNames.Pos);
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

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal? RoundNullable(decimal? value) => value.HasValue ? RoundMoney(value.Value) : null;

    private static decimal GetEffectiveSalesDiscountRate(
        decimal discount1,
        decimal discount2,
        decimal discount3,
        decimal discount4)
    {
        var remainingFactor = (1m - discount1 / 100m) *
            (1m - discount2 / 100m) *
            (1m - discount3 / 100m) *
            (1m - discount4 / 100m);
        return RoundMoney((1m - remainingFactor) * 100m);
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
