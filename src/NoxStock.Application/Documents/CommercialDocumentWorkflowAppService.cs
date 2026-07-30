using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.BusinessPartners;
using NoxStock.Permissions;
using NoxStock.MultiTenancy;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace NoxStock.Documents;

public class CommercialDocumentWorkflowAppService : ApplicationService, ICommercialDocumentWorkflowAppService
{
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IDocumentNumberGenerator _documentNumberGenerator;

    public CommercialDocumentWorkflowAppService(
        IRepository<Document, Guid> documentRepository,
        IRepository<BusinessPartner, Guid> businessPartnerRepository,
        IDocumentNumberGenerator documentNumberGenerator)
    {
        _documentRepository = documentRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _documentNumberGenerator = documentNumberGenerator;
    }

    [Authorize(NoxStockPermissions.Offers.Edit)]
    [UnitOfWork(true)]
    [NoxStock.Licensing.TenantLicenseWrite]
    public Task<DocumentDto> SendOfferAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        return SendAsync(id, DocumentType.Offer);
    }

    [Authorize(NoxStockPermissions.Offers.Convert)]
    [UnitOfWork(true)]
    [NoxStock.Licensing.TenantLicenseWrite]
    public Task<DocumentDto> ConvertOfferToOrderAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        return ConvertAsync(id, DocumentType.Offer, DocumentType.SaleOrder);
    }

    [Authorize(NoxStockPermissions.Offers.Convert)]
    [UnitOfWork(true)]
    [NoxStock.Licensing.TenantLicenseWrite]
    public Task<DocumentDto> ConvertOfferToInvoiceAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        return ConvertAsync(id, DocumentType.Offer, DocumentType.SaleInvoice);
    }

    [Authorize(NoxStockPermissions.Orders.Edit)]
    [UnitOfWork(true)]
    [NoxStock.Licensing.TenantLicenseWrite]
    public Task<DocumentDto> SendOrderAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        return SendAsync(id, null);
    }

    [Authorize(NoxStockPermissions.Orders.Convert)]
    [UnitOfWork(true)]
    [NoxStock.Licensing.TenantLicenseWrite]
    public async Task<DocumentDto> ConvertOrderToInvoiceAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        var source = await GetDocumentWithItemsAsync(id);
        var targetType = source.Type switch
        {
            DocumentType.SaleOrder => DocumentType.SaleInvoice,
            DocumentType.PurchaseOrder => DocumentType.PurchaseInvoice,
            _ => throw new BusinessException("NoxStock:DocumentIsNotOrder")
        };

        return await ConvertLoadedDocumentAsync(source, targetType);
    }

    private async Task<DocumentDto> SendAsync(Guid id, DocumentType? expectedType)
    {
        var document = await GetDocumentWithItemsAsync(id);
        if (expectedType.HasValue && document.Type != expectedType.Value)
        {
            throw new BusinessException("NoxStock:UnexpectedDocumentType");
        }

        if (document.Type is not (DocumentType.Offer or DocumentType.SaleOrder or DocumentType.PurchaseOrder))
        {
            throw new BusinessException("NoxStock:DocumentCannotBeSent");
        }

        if (document.Status != DocumentStatus.Draft)
        {
            throw new BusinessException("NoxStock:OnlyDraftDocumentsCanBeSent")
                .WithData("Status", document.Status);
        }

        document.TransitionTo(DocumentStatus.Sent);
        await _documentRepository.UpdateAsync(document, autoSave: true);
        return await MapDocumentAsync(document);
    }

    private async Task<DocumentDto> ConvertAsync(Guid id, DocumentType expectedSourceType, DocumentType targetType)
    {
        var source = await GetDocumentWithItemsAsync(id);
        if (source.Type != expectedSourceType)
        {
            throw new BusinessException("NoxStock:UnexpectedDocumentType");
        }

        return await ConvertLoadedDocumentAsync(source, targetType);
    }

    private async Task<DocumentDto> ConvertLoadedDocumentAsync(Document source, DocumentType targetType)
    {
        if (source.Status != DocumentStatus.Sent)
        {
            throw new BusinessException("NoxStock:OnlySentDocumentsCanBeConverted")
                .WithData("Status", source.Status);
        }

        if (source.Items.Count == 0)
        {
            throw new BusinessException("NoxStock:DocumentRequiresItems");
        }

        var existingTarget = await _documentRepository.FindAsync(x => x.SourceDocumentId == source.Id);
        if (existingTarget is not null)
        {
            throw new BusinessException("NoxStock:DocumentAlreadyConverted")
                .WithData("TargetDocumentId", existingTarget.Id);
        }

        var isTrySaleTarget = targetType is DocumentType.SaleOrder or DocumentType.SaleInvoice or DocumentType.Offer;
        var conversionFactor = isTrySaleTarget &&
            !string.Equals(source.Currency, "TRY", StringComparison.OrdinalIgnoreCase)
                ? source.ExchangeRate
                : 1m;
        var target = new Document(GuidGenerator.Create(), CurrentTenant.Id)
        {
            SourceDocumentId = source.Id,
            BusinessPartnerId = source.BusinessPartnerId,
            DocNumber = await _documentNumberGenerator.GetNextAsync(targetType),
            Type = targetType,
            Status = DocumentStatus.Draft,
            Subtotal = RoundMoney(source.Subtotal * conversionFactor),
            VatTotal = RoundMoney(source.VatTotal * conversionFactor),
            DiscountTotal = RoundMoney(source.DiscountTotal * conversionFactor),
            GrandTotal = RoundMoney(source.GrandTotal * conversionFactor),
            Currency = isTrySaleTarget ? "TRY" : source.Currency,
            ExchangeRate = isTrySaleTarget ? 1m : source.ExchangeRate,
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
                UnitPrice = RoundMoney(item.UnitPrice * conversionFactor),
                VatRate = item.VatRate,
                DiscountRate = item.DiscountRate,
                PurchaseDiscount1 = item.PurchaseDiscount1,
                PurchaseDiscount2 = item.PurchaseDiscount2,
                PurchaseDiscount3 = item.PurchaseDiscount3,
                PurchaseDiscount4 = item.PurchaseDiscount4,
                Total = RoundMoney(item.Total * conversionFactor)
            });
        }

        source.TransitionTo(DocumentStatus.Completed);
        await _documentRepository.UpdateAsync(source);
        await _documentRepository.InsertAsync(target, autoSave: true);
        return await MapDocumentAsync(target);
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 4, MidpointRounding.AwayFromZero);

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

    private async Task<DocumentDto> MapDocumentAsync(Document document)
    {
        var dto = ObjectMapper.Map<Document, DocumentDto>(document);
        var businessPartner = await _businessPartnerRepository.FindAsync(document.BusinessPartnerId);
        dto.BusinessPartnerName = businessPartner?.Name;
        return dto;
    }
}
