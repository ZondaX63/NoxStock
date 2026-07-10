using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using NexStock.BusinessPartners;
using NexStock.Permissions;
using NexStock.Products;

namespace NexStock.Documents;

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

    public DocumentAppService(
        IRepository<Document, Guid> repository,
        IRepository<BusinessPartner, Guid> businessPartnerRepository,
        IRepository<Product, Guid> productRepository,
        IRepository<DocumentItem, Guid> documentItemRepository)
        : base(repository)
    {
        _businessPartnerRepository = businessPartnerRepository;
        _productRepository = productRepository;
        _documentItemRepository = documentItemRepository;

        GetPolicyName = NexStockPermissions.Documents.Default;
        GetListPolicyName = NexStockPermissions.Documents.Default;
        CreatePolicyName = NexStockPermissions.Documents.Create;
        UpdatePolicyName = NexStockPermissions.Documents.Edit;
        DeletePolicyName = NexStockPermissions.Documents.Delete;
    }

    public override async Task<DocumentDto> CreateAsync(CreateUpdateDocumentDto input)
    {
        await CheckCreatePolicyAsync();

        var document = new Document(GuidGenerator.Create(), CurrentTenant.Id);
        ApplyInput(document, input);

        await Repository.InsertAsync(document, autoSave: true);
        return await GetAsync(document.Id);
    }

    public override async Task<DocumentDto> UpdateAsync(Guid id, CreateUpdateDocumentDto input)
    {
        await CheckUpdatePolicyAsync();

        var queryable = await Repository.WithDetailsAsync(x => x.Items);
        var document = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Id == id));
        if (document is null)
        {
            throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Document), id);
        }

        if (document.Status != DocumentStatus.Draft)
        {
            throw new Volo.Abp.BusinessException("NexStock:OnlyDraftDocumentsCanBeEdited");
        }

        foreach (var existingItem in document.Items.ToList())
        {
            await _documentItemRepository.DeleteAsync(existingItem);
        }

        document.Items.Clear();
        ApplyInput(document, input);

        await Repository.UpdateAsync(document, autoSave: true);
        return await GetAsync(document.Id);
    }

    public override async Task<DocumentDto> GetAsync(Guid id)
    {
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

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.ToLower();
            query = query.Where(x => x.DocNumber.ToLower().Contains(filter));
        }

        return query;
    }

    private void ApplyInput(Document document, CreateUpdateDocumentDto input)
    {
        document.BusinessPartnerId = input.BusinessPartnerId;
        document.DocNumber = input.DocNumber;
        document.Type = input.Type;
        document.Status = input.Status;
        document.Currency = input.Currency;
        document.ExchangeRate = input.ExchangeRate;
        document.Date = input.Date;
        document.DueDate = input.DueDate;
        document.Description = input.Description;
        document.PaidAmount = input.PaidAmount;

        foreach (var inputItem in input.Items)
        {
            var lineNet = inputItem.Quantity * inputItem.UnitPrice;
            var discount = lineNet * inputItem.DiscountRate / 100;
            var taxable = lineNet - discount;
            var vat = taxable * inputItem.VatRate / 100;

            document.Items.Add(new DocumentItem(GuidGenerator.Create(), CurrentTenant.Id)
            {
                ProductId = inputItem.ProductId,
                Quantity = inputItem.Quantity,
                UnitPrice = inputItem.UnitPrice,
                VatRate = inputItem.VatRate,
                DiscountRate = inputItem.DiscountRate,
                Total = taxable + vat
            });
        }

        if (document.Type is DocumentType.Collection or DocumentType.Payment)
        {
            document.GrandTotal = input.GrandTotal;
            document.Subtotal = input.GrandTotal;
            document.DiscountTotal = 0;
            document.VatTotal = 0;
        }
        else
        {
            document.Subtotal = input.Items.Sum(x => x.Quantity * x.UnitPrice);
            document.DiscountTotal = input.Items.Sum(x => x.Quantity * x.UnitPrice * x.DiscountRate / 100);
            document.VatTotal = input.Items.Sum(x =>
            {
                var lineNet = x.Quantity * x.UnitPrice;
                var discount = lineNet * x.DiscountRate / 100;
                return (lineNet - discount) * x.VatRate / 100;
            });
            document.GrandTotal = document.Subtotal - document.DiscountTotal + document.VatTotal;
        }
    }
}
