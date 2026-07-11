using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NexStock.BusinessPartners;
using NexStock.Permissions;
using NexStock.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;

namespace NexStock.Documents;

public class CommercialDocumentWorkflowAppService : ApplicationService, ICommercialDocumentWorkflowAppService
{
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly ISettingProvider _settingProvider;

    public CommercialDocumentWorkflowAppService(
        IRepository<Document, Guid> documentRepository,
        IRepository<BusinessPartner, Guid> businessPartnerRepository,
        ISettingProvider settingProvider)
    {
        _documentRepository = documentRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _settingProvider = settingProvider;
    }

    [Authorize(NexStockPermissions.Offers.Edit)]
    public Task<DocumentDto> SendOfferAsync(Guid id)
    {
        return SendAsync(id, DocumentType.Offer);
    }

    [Authorize(NexStockPermissions.Offers.Convert)]
    public Task<DocumentDto> ConvertOfferToOrderAsync(Guid id)
    {
        return ConvertAsync(id, DocumentType.Offer, DocumentType.SaleOrder);
    }

    [Authorize(NexStockPermissions.Offers.Convert)]
    public Task<DocumentDto> ConvertOfferToInvoiceAsync(Guid id)
    {
        return ConvertAsync(id, DocumentType.Offer, DocumentType.SaleInvoice);
    }

    [Authorize(NexStockPermissions.Orders.Edit)]
    public Task<DocumentDto> SendOrderAsync(Guid id)
    {
        return SendAsync(id, null);
    }

    [Authorize(NexStockPermissions.Orders.Convert)]
    public async Task<DocumentDto> ConvertOrderToInvoiceAsync(Guid id)
    {
        var source = await GetDocumentWithItemsAsync(id);
        var targetType = source.Type switch
        {
            DocumentType.SaleOrder => DocumentType.SaleInvoice,
            DocumentType.PurchaseOrder => DocumentType.PurchaseInvoice,
            _ => throw new BusinessException("NexStock:DocumentIsNotOrder")
        };

        return await ConvertLoadedDocumentAsync(source, targetType);
    }

    private async Task<DocumentDto> SendAsync(Guid id, DocumentType? expectedType)
    {
        var document = await GetDocumentWithItemsAsync(id);
        if (expectedType.HasValue && document.Type != expectedType.Value)
        {
            throw new BusinessException("NexStock:UnexpectedDocumentType");
        }

        if (document.Type is not (DocumentType.Offer or DocumentType.SaleOrder or DocumentType.PurchaseOrder))
        {
            throw new BusinessException("NexStock:DocumentCannotBeSent");
        }

        if (document.Status != DocumentStatus.Draft)
        {
            throw new BusinessException("NexStock:OnlyDraftDocumentsCanBeSent")
                .WithData("Status", document.Status);
        }

        document.Status = DocumentStatus.Sent;
        await _documentRepository.UpdateAsync(document, autoSave: true);
        return await MapDocumentAsync(document);
    }

    private async Task<DocumentDto> ConvertAsync(Guid id, DocumentType expectedSourceType, DocumentType targetType)
    {
        var source = await GetDocumentWithItemsAsync(id);
        if (source.Type != expectedSourceType)
        {
            throw new BusinessException("NexStock:UnexpectedDocumentType");
        }

        return await ConvertLoadedDocumentAsync(source, targetType);
    }

    private async Task<DocumentDto> ConvertLoadedDocumentAsync(Document source, DocumentType targetType)
    {
        if (source.Status != DocumentStatus.Sent)
        {
            throw new BusinessException("NexStock:OnlySentDocumentsCanBeConverted")
                .WithData("Status", source.Status);
        }

        if (source.Items.Count == 0)
        {
            throw new BusinessException("NexStock:DocumentRequiresItems");
        }

        var existingTarget = await _documentRepository.FindAsync(x => x.SourceDocumentId == source.Id);
        if (existingTarget is not null)
        {
            throw new BusinessException("NexStock:DocumentAlreadyConverted")
                .WithData("TargetDocumentId", existingTarget.Id);
        }

        var target = new Document(GuidGenerator.Create(), CurrentTenant.Id)
        {
            SourceDocumentId = source.Id,
            BusinessPartnerId = source.BusinessPartnerId,
            DocNumber = await CreateDocumentNumberAsync(targetType),
            Type = targetType,
            Status = DocumentStatus.Draft,
            Subtotal = source.Subtotal,
            VatTotal = source.VatTotal,
            DiscountTotal = source.DiscountTotal,
            GrandTotal = source.GrandTotal,
            Currency = source.Currency,
            ExchangeRate = source.ExchangeRate,
            Date = Clock.Now,
            DueDate = source.DueDate,
            Description = $"{source.DocNumber} belgesinden dönüştürüldü."
        };

        foreach (var item in source.Items)
        {
            target.Items.Add(new DocumentItem(GuidGenerator.Create(), CurrentTenant.Id)
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                VatRate = item.VatRate,
                DiscountRate = item.DiscountRate,
                Total = item.Total
            });
        }

        source.Status = DocumentStatus.Completed;
        await _documentRepository.UpdateAsync(source);
        await _documentRepository.InsertAsync(target, autoSave: true);
        return await MapDocumentAsync(target);
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

    private async Task<string> CreateDocumentNumberAsync(DocumentType type)
    {
        var settingName = type switch
        {
            DocumentType.SaleOrder => NexStockSettings.SaleOrderNumberPrefix,
            DocumentType.PurchaseOrder => NexStockSettings.PurchaseOrderNumberPrefix,
            DocumentType.SaleInvoice => NexStockSettings.SaleInvoiceNumberPrefix,
            DocumentType.PurchaseInvoice => NexStockSettings.PurchaseInvoiceNumberPrefix,
            _ => string.Empty
        };

        var prefix = string.IsNullOrEmpty(settingName)
            ? "BEL"
            : await _settingProvider.GetOrNullAsync(settingName) ?? "BEL";

        return $"{prefix}-{Clock.Now:yyyyMMdd-HHmmss}-{GuidGenerator.Create().ToString("N")[..6].ToUpperInvariant()}";
    }

    private async Task<DocumentDto> MapDocumentAsync(Document document)
    {
        var dto = ObjectMapper.Map<Document, DocumentDto>(document);
        var businessPartner = await _businessPartnerRepository.FindAsync(document.BusinessPartnerId);
        dto.BusinessPartnerName = businessPartner?.Name;
        return dto;
    }
}
