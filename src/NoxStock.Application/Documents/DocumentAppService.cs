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
using NoxStock.Permissions;
using NoxStock.Products;
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

    public DocumentAppService(
        IRepository<Document, Guid> repository,
        IRepository<BusinessPartner, Guid> businessPartnerRepository,
        IRepository<Product, Guid> productRepository,
        IRepository<DocumentItem, Guid> documentItemRepository,
        IProductPriceCalculator priceCalculator,
        IDocumentNumberGenerator documentNumberGenerator,
        IDataFilter dataFilter)
        : base(repository)
    {
        _businessPartnerRepository = businessPartnerRepository;
        _productRepository = productRepository;
        _documentItemRepository = documentItemRepository;
        _priceCalculator = priceCalculator;
        _documentNumberGenerator = documentNumberGenerator;
        _dataFilter = dataFilter;

        GetPolicyName = NoxStockPermissions.Documents.Default;
        GetListPolicyName = NoxStockPermissions.Documents.Default;
        CreatePolicyName = NoxStockPermissions.Documents.Create;
        UpdatePolicyName = NoxStockPermissions.Documents.Edit;
        DeletePolicyName = NoxStockPermissions.Documents.Delete;
    }

    public override async Task<DocumentDto> CreateAsync(CreateUpdateDocumentDto input)
    {
        var tenantId = CurrentTenant.RequireTenantId();
        await CheckCreatePolicyAsync();

        var document = new Document(GuidGenerator.Create(), tenantId);
        await ApplyInputAsync(document, input, isNewDocument: true);

        await Repository.InsertAsync(document, autoSave: true);
        return await GetAsync(document.Id);
    }

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

        foreach (var existingItem in document.Items.ToList())
        {
            await _documentItemRepository.DeleteAsync(existingItem);
        }

        document.Items.Clear();
        await ApplyInputAsync(document, input, isNewDocument: false);

        await Repository.UpdateAsync(document, autoSave: true);
        return await GetAsync(document.Id);
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
            var filter = input.Filter.ToLower();
            query = query.Where(x => x.DocNumber.ToLower().Contains(filter));
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
        document.Currency = string.IsNullOrWhiteSpace(input.Currency) ? "TRY" : input.Currency.Trim().ToUpperInvariant();
        document.ExchangeRate = await _priceCalculator.GetExchangeRateAsync(document.Currency);
        document.Date = input.Date;
        document.DueDate = input.DueDate;
        document.Description = input.Description?.Trim();
        document.PaidAmount = isNewDocument ? 0 : document.PaidAmount;

        foreach (var inputItem in input.Items)
        {
            var product = await _productRepository.FindAsync(inputItem.ProductId);
            if (product is null)
            {
                throw new BusinessException("NoxStock:InvalidProductReference");
            }

            var unitPrice = inputItem.UnitPrice;
            if (IsSalesDocument(document.Type) && !inputItem.IsUnitPriceOverridden)
            {
                if (product.UseCurrentExchangeRateForSale)
                {
                    unitPrice = (await _priceCalculator.CalculateAsync(product, document.Currency)).UnitPrice;
                }
            }

            EnsureItemIsValid(inputItem);

            var lineNet = inputItem.Quantity * unitPrice;
            var discount = lineNet * inputItem.DiscountRate / 100;
            var taxable = lineNet - discount;
            var vat = taxable * inputItem.VatRate / 100;

            document.Items.Add(new DocumentItem(GuidGenerator.Create(), CurrentTenant.Id)
            {
                ProductId = inputItem.ProductId,
                Quantity = inputItem.Quantity,
                UnitPrice = unitPrice,
                VatRate = inputItem.VatRate,
                DiscountRate = inputItem.DiscountRate,
                Total = RoundMoney(taxable + vat)
            });
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
        if (item.Quantity <= 0 || item.UnitPrice < 0 || item.VatRate is < 0 or > 100 || item.DiscountRate is < 0 or > 100)
        {
            throw new BusinessException("NoxStock:InvalidDocumentItem");
        }
    }

    private static decimal RoundMoney(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private static bool IsSalesDocument(DocumentType type)
    {
        return type is DocumentType.SaleInvoice or DocumentType.Offer or DocumentType.SaleOrder;
    }
}
