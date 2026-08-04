using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using NoxStock.BusinessPartners;
using NoxStock.ExchangeRates;
using NoxStock.Permissions;
using NoxStock.Products;
using NoxStock.Common;
using NoxStock.MultiTenancy;
using Volo.Abp;

namespace NoxStock.Documents;

[Authorize(NoxStockPermissions.Documents.Default)]
public class DocumentAppService :
    CrudAppService<
        Document,
        DocumentDto,
        Guid,
        DocumentGetListInput,
        CreateUpdateDocumentDto>,
    IDocumentAppService
{
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<DocumentItem, Guid> _documentItemRepository;
    private readonly IProductPriceCalculator _priceCalculator;
    private readonly IDocumentNumberGenerator _documentNumberGenerator;
    private readonly IDataFilter _dataFilter;
    private readonly IRepository<DocumentExchangeRateSnapshot, Guid> _exchangeRateSnapshotRepository;
    private readonly IRepository<ExchangeRate, Guid> _exchangeRateRepository;
    private readonly NoxStock.Licensing.ITenantLicenseAccessChecker _licenseAccessChecker;

    public DocumentAppService(
        IRepository<Document, Guid> repository,
        IRepository<BusinessPartner, Guid> businessPartnerRepository,
        IRepository<Product, Guid> productRepository,
        IRepository<DocumentItem, Guid> documentItemRepository,
        IProductPriceCalculator priceCalculator,
        IDocumentNumberGenerator documentNumberGenerator,
        IDataFilter dataFilter,
        IRepository<DocumentExchangeRateSnapshot, Guid> exchangeRateSnapshotRepository,
        IRepository<ExchangeRate, Guid> exchangeRateRepository,
        NoxStock.Licensing.ITenantLicenseAccessChecker licenseAccessChecker)
        : base(repository)
    {
        _businessPartnerRepository = businessPartnerRepository;
        _productRepository = productRepository;
        _documentItemRepository = documentItemRepository;
        _priceCalculator = priceCalculator;
        _documentNumberGenerator = documentNumberGenerator;
        _dataFilter = dataFilter;
        _exchangeRateSnapshotRepository = exchangeRateSnapshotRepository;
        _exchangeRateRepository = exchangeRateRepository;
        _licenseAccessChecker = licenseAccessChecker;

        GetPolicyName = NoxStockPermissions.Documents.Default;
        GetListPolicyName = NoxStockPermissions.Documents.Default;
        CreatePolicyName = NoxStockPermissions.Documents.Create;
        UpdatePolicyName = NoxStockPermissions.Documents.Edit;
        DeletePolicyName = NoxStockPermissions.Documents.Delete;
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    public override async Task<DocumentDto> CreateAsync(CreateUpdateDocumentDto input)
    {
        var tenantId = CurrentTenant.RequireTenantId();
        EnsureSalesCurrencyIsTry(input.Type, input.Currency);
        await EnsureCurrencyEntitlementAsync(IsTryOnlyDocument(input.Type) ? "TRY" : input.Currency);
        await CheckCreatePolicyAsync();

        var document = new Document(GuidGenerator.Create(), tenantId);
        await ApplyInputAsync(document, input, isNewDocument: true);

        await Repository.InsertAsync(document, autoSave: true);
        await RefreshExchangeRateSnapshotsAsync(document);
        return await GetAsync(document.Id);
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    public override async Task<DocumentDto> UpdateAsync(Guid id, CreateUpdateDocumentDto input)
    {
        CurrentTenant.RequireTenantId();
        await CheckUpdatePolicyAsync();

        var queryable = await Repository.WithDetailsAsync(x => x.Items);
        var document = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Id == id));
        if (document is null)
        {
            throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Document), id);
        }

        if (document.Status != DocumentStatus.Draft)
        {
            throw new Volo.Abp.BusinessException("NoxStock:OnlyDraftDocumentsCanBeEdited");
        }

        EnsureSalesCurrencyIsTry(document.Type, input.Currency);
        await EnsureCurrencyEntitlementAsync(IsTryOnlyDocument(document.Type) ? "TRY" : input.Currency);

        foreach (var existingItem in document.Items.ToList())
        {
            await _documentItemRepository.DeleteAsync(existingItem);
        }

        document.Items.Clear();
        await ApplyInputAsync(document, input, isNewDocument: false);

        await Repository.UpdateAsync(document, autoSave: true);
        await RefreshExchangeRateSnapshotsAsync(document);
        return await GetAsync(document.Id);
    }

    private Task EnsureCurrencyEntitlementAsync(string? currency)
    {
        return string.Equals(currency?.Trim(), "TRY", StringComparison.OrdinalIgnoreCase)
            ? Task.CompletedTask
            : _licenseAccessChecker.EnsureFeatureAsync(
                NoxStock.Licensing.LicenseFeatureNames.MultiCurrency);
    }

    public override async Task<DocumentDto> GetAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        // Must override to include Items and Map BusinessPartner/Product Names
        var queryable = await Repository.WithDetailsAsync(x => x.Items);
        var document = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Id == id));

        if (document == null)
        {
            throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Document), id);
        }

        var documentDto = ObjectMapper.Map<Document, DocumentDto>(document);

        // Fetch BusinessPartnerName
        var businessPartner = await _businessPartnerRepository.FindAsync(document.BusinessPartnerId);
        if (businessPartner != null)
        {
            documentDto.BusinessPartnerName = businessPartner.Name;
        }

        // Fetch ProductNames for Items
        var productIds = document.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await _productRepository.GetListAsync(x => productIds.Contains(x.Id));
        var productDictionary = products.ToDictionary(x => x.Id, x => x.Name);

        foreach (var item in documentDto.Items)
        {
            if (productDictionary.TryGetValue(item.ProductId, out var productName))
            {
                item.ProductName = productName;
            }
        }

        return documentDto;
    }

    public override async Task<PagedResultDto<DocumentDto>> GetListAsync(DocumentGetListInput input)
    {
        CurrentTenant.RequireTenantId();
        input.SkipCount = Math.Clamp(input.SkipCount, 0, 10_000);
        input.MaxResultCount = Math.Clamp(input.MaxResultCount, 1, 100);
        var query = await CreateFilteredQueryAsync(input);
        var totalCount = await AsyncExecuter.CountAsync(query);

        query = ApplySorting(query, input);
        query = ApplyPaging(query, input);

        var documents = await AsyncExecuter.ToListAsync(query);
        var documentDtos = documents
            .Select(ObjectMapper.Map<Document, DocumentDto>)
            .ToList();

        var businessPartnerIds = documents.Select(x => x.BusinessPartnerId).Distinct().ToList();
        var businessPartners = await _businessPartnerRepository.GetListAsync(x => businessPartnerIds.Contains(x.Id));
        var businessPartnerNames = businessPartners.ToDictionary(x => x.Id, x => x.Name);

        foreach (var documentDto in documentDtos)
        {
            if (businessPartnerNames.TryGetValue(documentDto.BusinessPartnerId, out var businessPartnerName))
            {
                documentDto.BusinessPartnerName = businessPartnerName;
            }
        }

        return new PagedResultDto<DocumentDto>(totalCount, documentDtos);
    }

    public async Task<string> GetSuggestedNumberAsync(DocumentType type)
    {
        CurrentTenant.RequireTenantId();
        await CheckCreatePolicyAsync();
        return await _documentNumberGenerator.GetNextAsync(type);
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    public override async Task DeleteAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        await CheckDeletePolicyAsync();
        var document = await Repository.GetAsync(id);
        if (document.Status != DocumentStatus.Draft)
        {
            throw new BusinessException("NoxStock:OnlyDraftDocumentsCanBeDeleted");
        }

        await Repository.DeleteAsync(document, autoSave: true);
    }

    protected override async Task<IQueryable<Document>> CreateFilteredQueryAsync(DocumentGetListInput input)
    {
        var query = await base.CreateFilteredQueryAsync(input);

        if (input.Type.HasValue)
        {
            query = query.Where(x => x.Type == input.Type.Value);
        }

        if (input.BusinessPartnerId.HasValue)
        {
            query = query.Where(x => x.BusinessPartnerId == input.BusinessPartnerId.Value);
        }

        if (input.Scope == DocumentListScope.InvoicesOnly)
        {
            query = query.Where(x => x.Type == DocumentType.SaleInvoice || x.Type == DocumentType.PurchaseInvoice);
        }
        else if (input.Scope == DocumentListScope.OffersOnly)
        {
            query = query.Where(x => x.Type == DocumentType.Offer);
        }
        else if (input.Scope == DocumentListScope.OrdersOnly)
        {
            query = query.Where(x => x.Type == DocumentType.SaleOrder || x.Type == DocumentType.PurchaseOrder);
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = SearchText.Normalize(input.Filter);
            query = query.Where(x => x.DocNumber.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(filter));
        }

        if (input.DueFilter != DocumentDueFilter.All)
        {
            var today = Clock.Now.Date;
            query = query.Where(x =>
                (x.Type == DocumentType.SaleInvoice || x.Type == DocumentType.PurchaseInvoice) &&
                (x.Status == DocumentStatus.Approved || x.Status == DocumentStatus.PartiallyPaid) &&
                x.DueDate.HasValue);

            if (input.DueFilter == DocumentDueFilter.Overdue)
            {
                query = query.Where(x => x.DueDate!.Value < today);
            }
            else if (input.DueFilter == DocumentDueFilter.DueSoon)
            {
                var dueSoonDate = today.AddDays(7);
                query = query.Where(x => x.DueDate!.Value >= today && x.DueDate!.Value <= dueSoonDate);
            }
        }

        return query;
    }

    private async Task ApplyInputAsync(Document document, CreateUpdateDocumentDto input, bool isNewDocument)
    {
        var businessPartner = await _businessPartnerRepository.FindAsync(input.BusinessPartnerId);
        if (businessPartner is null)
        {
            throw new BusinessException("NoxStock:InvalidBusinessPartnerReference");
        }

        var type = isNewDocument ? input.Type : document.Type;
        EnsureBusinessPartnerMatchesDocumentType(businessPartner, type);
        EnsureDatesAreValid(input.Date, input.DueDate);

        if (type is not (DocumentType.Collection or DocumentType.Payment) && input.Items.Count == 0)
        {
            throw new BusinessException("NoxStock:DocumentRequiresItems");
        }

        document.BusinessPartnerId = input.BusinessPartnerId;
        document.Type = type;
        document.DocNumber = await GetDocumentNumberAsync(document, input.DocNumber);
        document.Status = isNewDocument ? DocumentStatus.Draft : document.Status;
        document.Currency = IsTryOnlyDocument(type)
            ? "TRY"
            : string.IsNullOrWhiteSpace(input.Currency) ? "TRY" : input.Currency.Trim().ToUpperInvariant();
        var currentRate = await _priceCalculator.GetExchangeRateAsync(document.Currency);
        // The DTO's neutral default is 1 for TRY. On a newly-created foreign
        // purchase it means "rate not supplied", so use the document-date
        // rate. A positive non-neutral value is an explicit historical override.
        var hasExplicitPurchaseRate = IsPurchaseDocument(type) &&
            document.Currency != "TRY" &&
            input.ExchangeRate > 0m &&
            (!isNewDocument || input.ExchangeRate != 1m);
        document.ExchangeRate = hasExplicitPurchaseRate ? input.ExchangeRate : currentRate;
        document.Date = input.Date;
        document.DueDate = input.DueDate;
        document.Description = input.Description?.Trim();
        document.PaidAmount = isNewDocument ? 0 : document.PaidAmount;

        var productIds = input.Items
            .Select(item => item.ProductId)
            .Distinct()
            .ToList();
        var productsById = (await _productRepository.GetListAsync(product => productIds.Contains(product.Id)))
            .ToDictionary(product => product.Id);
        if (productsById.Count != productIds.Count)
        {
            throw new BusinessException("NoxStock:InvalidProductReference");
        }

        var priceCalculationsByProductId = new Dictionary<Guid, ProductPriceCalculation>();
        foreach (var inputItem in input.Items)
        {
            var product = productsById[inputItem.ProductId];

            var unitPrice = inputItem.UnitPrice;
            ProductPriceCalculation? priceCalculation = null;
            var usesDynamicPricing = product.PricingMode == ProductPricingMode.DynamicCurrencyMarkup ||
                product.UseCurrentExchangeRateForSale;
            if (IsSalesDocument(document.Type))
            {
                if (!priceCalculationsByProductId.TryGetValue(product.Id, out priceCalculation))
                {
                    priceCalculation = await _priceCalculator.CalculateAsync(product);
                    priceCalculationsByProductId[product.Id] = priceCalculation;
                }
                if (usesDynamicPricing && !priceCalculation.IsAvailable)
                {
                    throw new BusinessException(priceCalculation.UnavailableReason ?? "NoxStock:ProductApprovedPurchaseCostUnavailable")
                        .WithData("ProductName", product.Name);
                }

                if (!inputItem.IsUnitPriceOverridden && usesDynamicPricing && priceCalculation.IsAvailable)
                {
                    // A document line keeps the KDV-hariç list price. The
                    // product's four sales discounts are stored separately
                    // and applied once when calculating the taxable amount.
                    unitPrice = priceCalculation.ListSalePriceInTry;
                }
                else if (unitPrice <= 0m)
                {
                    unitPrice = product.SalePrice;
                }

                ApplyBusinessPartnerSalesDiscountDefaults(inputItem, businessPartner);
                if (inputItem.VatRate == 0m && product.VatRate > 0)
                {
                    inputItem.VatRate = product.VatRate;
                }
            }
            else if (IsPurchaseDocument(document.Type) && unitPrice <= 0m)
            {
                unitPrice = product.PurchaseListPrice > 0m
                    ? product.PurchaseListPrice
                    : product.PurchasePrice;
            }

            EnsureItemIsValid(inputItem);

            var effectiveDiscountRate = IsPurchaseDocument(document.Type)
                ? GetEffectivePurchaseDiscountRate(inputItem)
                : IsSalesDocument(document.Type)
                    ? GetEffectiveSalesDiscountRate(inputItem)
                    : inputItem.DiscountRate;
            var lineNet = inputItem.Quantity * unitPrice;
            var discount = lineNet * effectiveDiscountRate / 100;
            var taxable = lineNet - discount;
            var vat = taxable * inputItem.VatRate / 100;

            var documentItem = new DocumentItem(GuidGenerator.Create(), CurrentTenant.Id)
            {
                ProductId = inputItem.ProductId,
                Quantity = inputItem.Quantity,
                UnitPrice = unitPrice,
                VatRate = inputItem.VatRate,
                DiscountRate = effectiveDiscountRate,
                PurchaseDiscount1 = inputItem.PurchaseDiscount1,
                PurchaseDiscount2 = inputItem.PurchaseDiscount2,
                PurchaseDiscount3 = inputItem.PurchaseDiscount3,
                PurchaseDiscount4 = inputItem.PurchaseDiscount4,
                SalesDiscount1 = inputItem.SalesDiscount1,
                SalesDiscount2 = inputItem.SalesDiscount2,
                SalesDiscount3 = inputItem.SalesDiscount3,
                SalesDiscount4 = inputItem.SalesDiscount4,
                Total = RoundMoney(taxable + vat),
                IsUnitPriceOverridden = inputItem.IsUnitPriceOverridden,
                PriceCurrencyCode = priceCalculation?.Currency ?? document.Currency,
                PriceExchangeRate = priceCalculation?.ExchangeRate ?? document.ExchangeRate,
                ProductCostHistoryId = priceCalculation?.ProductCostHistoryId ?? product.LastPurchaseCostHistoryId,
                PricingMode = inputItem.IsUnitPriceOverridden
                    ? ProductPricingMode.Manual
                    : priceCalculation is not null
                        ? product.PricingMode
                        : null
            };
            ApplyPricingSnapshot(documentItem, product, priceCalculation, lockDate: null);
            document.Items.Add(documentItem);
        }

        if (document.Type is DocumentType.Collection or DocumentType.Payment)
        {
            if (input.GrandTotal <= 0)
            {
                throw new BusinessException("NoxStock:DocumentTotalMustBePositive");
            }

            document.GrandTotal = RoundMoney(input.GrandTotal);
            document.Subtotal = document.GrandTotal;
            document.DiscountTotal = 0;
            document.VatTotal = 0;
        }
        else
        {
            document.Subtotal = RoundMoney(document.Items.Sum(x => x.Quantity * x.UnitPrice));
            document.DiscountTotal = RoundMoney(document.Items.Sum(x => x.Quantity * x.UnitPrice * x.DiscountRate / 100));
            document.VatTotal = RoundMoney(document.Items.Sum(x =>
            {
                var lineNet = x.Quantity * x.UnitPrice;
                var discount = lineNet * x.DiscountRate / 100;
                return (lineNet - discount) * x.VatRate / 100;
            }));
            document.GrandTotal = RoundMoney(document.Subtotal - document.DiscountTotal + document.VatTotal);
        }
    }

    private async Task RefreshExchangeRateSnapshotsAsync(Document document)
    {
        var existingSnapshots = await _exchangeRateSnapshotRepository.GetListAsync(x => x.DocumentId == document.Id);
        foreach (var snapshot in existingSnapshots)
        {
            await _exchangeRateSnapshotRepository.DeleteAsync(snapshot);
        }

        var currentRates = await _exchangeRateRepository.GetListAsync();
        var snapshotCurrencies = currentRates
            .Where(x => x.Rate > 0)
            .Select(x => x.Currency.Trim().ToUpperInvariant())
            .Append("TRY")
            .Append(document.Currency.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var currency in snapshotCurrencies)
        {
            var currentRate = currentRates.FirstOrDefault(x =>
                string.Equals(x.Currency, currency, StringComparison.OrdinalIgnoreCase));
            var isDocumentCurrency = string.Equals(currency, document.Currency, StringComparison.OrdinalIgnoreCase);
            var rateToTry = currency == "TRY"
                ? 1m
                : isDocumentCurrency && document.ExchangeRate > 0m
                    ? document.ExchangeRate
                    : currentRate?.Rate ?? 0m;
            if (rateToTry <= 0m)
            {
                continue;
            }
            await _exchangeRateSnapshotRepository.InsertAsync(new DocumentExchangeRateSnapshot(
                GuidGenerator.Create(), CurrentTenant.Id, document.Id,
                currency, rateToTry, isDocumentCurrency ? document.Date : currentRate?.RateDate ?? document.Date));
        }
    }

    private async Task<string> GetDocumentNumberAsync(Document document, string? requestedNumber)
    {
        var number = string.IsNullOrWhiteSpace(requestedNumber)
            ? await _documentNumberGenerator.GetNextAsync(document.Type)
            : requestedNumber.Trim().ToUpperInvariant();
        Document? existing;
        using (_dataFilter.Disable<ISoftDelete>())
        {
            existing = await Repository.FindAsync(x =>
                x.Id != document.Id && x.Type == document.Type && x.DocNumber == number);
        }

        if (existing is not null)
        {
            throw new BusinessException("NoxStock:DocumentNumberAlreadyExists")
                .WithData("DocNumber", number);
        }

        return number;
    }

    private static void EnsureBusinessPartnerMatchesDocumentType(BusinessPartner businessPartner, DocumentType type)
    {
        var requiresCustomer = type is DocumentType.SaleInvoice or DocumentType.Offer or DocumentType.SaleOrder or DocumentType.Collection;
        var requiresSupplier = type is DocumentType.PurchaseInvoice or DocumentType.PurchaseOrder or DocumentType.Payment;

        if ((requiresCustomer && businessPartner.Type is not (BusinessPartnerType.Customer or BusinessPartnerType.Both)) ||
            (requiresSupplier && businessPartner.Type is not (BusinessPartnerType.Supplier or BusinessPartnerType.Both)))
        {
            throw new BusinessException("NoxStock:BusinessPartnerTypeDoesNotMatchDocument");
        }
    }

    private static void EnsureDatesAreValid(DateTime date, DateTime? dueDate)
    {
        if (dueDate.HasValue && dueDate.Value.Date < date.Date)
        {
            throw new BusinessException("NoxStock:DueDateCannotBeEarlierThanDocumentDate");
        }
    }

    private static void EnsureItemIsValid(CreateUpdateDocumentItemDto item)
    {
        var percentages = new[]
        {
            item.DiscountRate,
            item.PurchaseDiscount1,
            item.PurchaseDiscount2,
            item.PurchaseDiscount3,
            item.PurchaseDiscount4,
            item.SalesDiscount1,
            item.SalesDiscount2,
            item.SalesDiscount3,
            item.SalesDiscount4
        };
        if (item.Quantity <= 0 || item.UnitPrice < 0 || item.VatRate is < 0 or > 100 ||
            percentages.Any(value => value is < 0 or > 100))
        {
            throw new BusinessException("NoxStock:InvalidDocumentItem");
        }
    }

    private static decimal RoundMoney(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private static void ApplyPricingSnapshot(
        DocumentItem item,
        Product product,
        ProductPriceCalculation? calculation,
        DateTime? lockDate)
    {
        if (calculation is null)
        {
            return;
        }

        item.PriceCurrencyCode = calculation.Currency;
        item.PriceExchangeRate = calculation.ExchangeRate;
        item.PriceRateDate = calculation.ReferenceCurrencyRateDate ?? calculation.RateDate;
        item.UnitPriceTry = RoundMoney(item.UnitPrice);
        item.ProductCostHistoryId = calculation.ProductCostHistoryId ?? product.LastPurchaseCostHistoryId;
        item.CostCurrencyCode = product.LastPurchaseCostCurrencyCode;
        item.CostCurrencyAmount = product.LastPurchaseCostCurrencyAmount;
        item.CostTryAmount = calculation.NetCostInTry > 0m
            ? calculation.NetCostInTry
            : product.LastPurchaseCostTryAmount;
        item.AppliedMarkupPercent = calculation.MarkupPercent;
        item.ReferenceCurrencyCode = calculation.ReferenceCurrencyCode;
        item.ReferenceUnitPrice = calculation.ReferenceSaleUnitPrice;
        item.ReferenceExchangeRate = calculation.ReferenceCurrencyExchangeRate;
        item.PricingSnapshotDate = lockDate;
    }

    private static bool IsSalesDocument(DocumentType type)
    {
        return type is DocumentType.SaleInvoice or DocumentType.Offer or DocumentType.SaleOrder;
    }

    private static bool IsPurchaseDocument(DocumentType type)
    {
        return type is DocumentType.PurchaseInvoice or DocumentType.PurchaseOrder;
    }

    private static bool IsTryOnlyDocument(DocumentType type)
    {
        return type is DocumentType.SaleInvoice or DocumentType.SaleOrder or DocumentType.Offer or
            DocumentType.Collection or DocumentType.PosSale;
    }

    private static void EnsureSalesCurrencyIsTry(DocumentType type, string? currency)
    {
        if (IsTryOnlyDocument(type) &&
            !string.IsNullOrWhiteSpace(currency) &&
            !string.Equals(currency.Trim(), "TRY", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("NoxStock:SalesDocumentsRequireTry");
        }
    }

    private static decimal GetEffectivePurchaseDiscountRate(CreateUpdateDocumentItemDto item)
    {
        var hasDiscountChain = item.PurchaseDiscount1 != 0m || item.PurchaseDiscount2 != 0m ||
            item.PurchaseDiscount3 != 0m || item.PurchaseDiscount4 != 0m;
        if (!hasDiscountChain)
        {
            return item.DiscountRate;
        }

        var remainingFactor = (1m - item.PurchaseDiscount1 / 100m) *
            (1m - item.PurchaseDiscount2 / 100m) *
            (1m - item.PurchaseDiscount3 / 100m) *
            (1m - item.PurchaseDiscount4 / 100m);
        return RoundMoney((1m - remainingFactor) * 100m);
    }

    private static void ApplyBusinessPartnerSalesDiscountDefaults(CreateUpdateDocumentItemDto item, BusinessPartner businessPartner)
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

    private static decimal GetEffectiveSalesDiscountRate(CreateUpdateDocumentItemDto item)
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
}
