using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace NoxStock.Documents;

public class Document : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    
    public Guid BusinessPartnerId { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public string DocNumber { get; set; }
    public DocumentType Type { get; set; }
    public DocumentStatus Status { get; set; }
    public decimal Subtotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal? LegacyPaidAmountBaseline { get; protected set; }
    public string Currency { get; set; }
    public decimal ExchangeRate { get; set; }
    public DateTime Date { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Description { get; set; }
    public string? OperationKey { get; set; }

    // Identity data used to render a finalized commercial document. These values
    // are deliberately separate from the mutable company and partner records.
    public DateTime? PrintSnapshotCapturedAt { get; protected set; }
    public string? CompanyNameSnapshot { get; protected set; }
    public string? CompanyAddressSnapshot { get; protected set; }
    public string? CompanyPhoneSnapshot { get; protected set; }
    public string? CompanyEmailSnapshot { get; protected set; }
    public string? CompanyTaxOfficeSnapshot { get; protected set; }
    public string? CompanyTaxNumberSnapshot { get; protected set; }
    public string? BusinessPartnerNameSnapshot { get; protected set; }
    public string? BusinessPartnerAddressSnapshot { get; protected set; }
    public string? BusinessPartnerTaxOfficeSnapshot { get; protected set; }
    public string? BusinessPartnerTaxNumberSnapshot { get; protected set; }

    public ICollection<DocumentItem> Items { get; set; }
    public ICollection<DocumentExchangeRateSnapshot> ExchangeRateSnapshots { get; set; }

    public Document()
    {
        Items = new List<DocumentItem>();
        ExchangeRateSnapshots = new List<DocumentExchangeRateSnapshot>();
        DocNumber = string.Empty;
        Currency = "TRY";
        ExchangeRate = 1;
        Date = DateTime.UtcNow;
    }

    public Document(Guid id, Guid? tenantId)
        : base(id)
    {
        TenantId = tenantId;
        DocNumber = string.Empty;
        Items = new List<DocumentItem>();
        ExchangeRateSnapshots = new List<DocumentExchangeRateSnapshot>();
        Currency = "TRY";
        ExchangeRate = 1;
        Date = DateTime.UtcNow;
    }

    public void TransitionTo(DocumentStatus targetStatus)
    {
        if (Status == targetStatus)
        {
            return;
        }

        var isAllowed = Status switch
        {
            DocumentStatus.Draft => targetStatus is DocumentStatus.Sent or DocumentStatus.Approved or
                DocumentStatus.PartiallyPaid or DocumentStatus.Paid or DocumentStatus.Completed,
            DocumentStatus.Sent => targetStatus is DocumentStatus.Approved or DocumentStatus.PartiallyPaid or
                DocumentStatus.Paid or DocumentStatus.Completed,
            DocumentStatus.Approved => targetStatus is DocumentStatus.PartiallyPaid or DocumentStatus.Paid or
                DocumentStatus.Cancelled,
            DocumentStatus.PartiallyPaid => targetStatus is DocumentStatus.Paid or DocumentStatus.Cancelled,
            DocumentStatus.Paid => targetStatus == DocumentStatus.Cancelled,
            DocumentStatus.Completed => targetStatus == DocumentStatus.Cancelled,
            _ => false
        };

        if (!isAllowed)
        {
            throw new BusinessException("NoxStock:InvalidDocumentStatusTransition")
                .WithData("CurrentStatus", Status)
                .WithData("TargetStatus", targetStatus);
        }

        Status = targetStatus;
    }

    public void CapturePrintSnapshot(
        DateTime capturedAt,
        string companyName,
        string companyAddress,
        string companyPhone,
        string companyEmail,
        string companyTaxOffice,
        string companyTaxNumber,
        string businessPartnerName,
        string? businessPartnerAddress,
        string? businessPartnerTaxOffice,
        string? businessPartnerTaxNumber)
    {
        if (PrintSnapshotCapturedAt.HasValue)
        {
            throw new BusinessException("NoxStock:DocumentPrintSnapshotAlreadyCaptured");
        }

        PrintSnapshotCapturedAt = capturedAt;
        CompanyNameSnapshot = companyName;
        CompanyAddressSnapshot = companyAddress;
        CompanyPhoneSnapshot = companyPhone;
        CompanyEmailSnapshot = companyEmail;
        CompanyTaxOfficeSnapshot = companyTaxOffice;
        CompanyTaxNumberSnapshot = companyTaxNumber;
        BusinessPartnerNameSnapshot = businessPartnerName;
        BusinessPartnerAddressSnapshot = businessPartnerAddress;
        BusinessPartnerTaxOfficeSnapshot = businessPartnerTaxOffice;
        BusinessPartnerTaxNumberSnapshot = businessPartnerTaxNumber;
    }

    public void SetPaidAmountFromAllocations(decimal paidAmount)
    {
        if (Type is not (DocumentType.SaleInvoice or DocumentType.PurchaseInvoice))
        {
            throw new BusinessException("NoxStock:DocumentTypeDoesNotSupportPayment");
        }

        if (paidAmount < 0m || paidAmount > GrandTotal)
        {
            throw new BusinessException("NoxStock:InvalidAllocatedAmount");
        }

        PaidAmount = paidAmount;
        if (Status == DocumentStatus.Cancelled)
        {
            return;
        }

        Status = paidAmount >= GrandTotal && paidAmount > 0m
            ? DocumentStatus.Paid
            : paidAmount > 0m
                ? DocumentStatus.PartiallyPaid
                : DocumentStatus.Approved;
    }

    public void CaptureLegacyPaidAmountBaseline()
    {
        if (!LegacyPaidAmountBaseline.HasValue && PaidAmount > 0m)
        {
            LegacyPaidAmountBaseline = PaidAmount;
        }
    }

    public void CopyPrintSnapshotFrom(Document source)
    {
        CapturePrintSnapshot(source.PrintSnapshotCapturedAt ?? DateTime.UtcNow,
            source.CompanyNameSnapshot ?? string.Empty, source.CompanyAddressSnapshot ?? string.Empty,
            source.CompanyPhoneSnapshot ?? string.Empty, source.CompanyEmailSnapshot ?? string.Empty,
            source.CompanyTaxOfficeSnapshot ?? string.Empty, source.CompanyTaxNumberSnapshot ?? string.Empty,
            source.BusinessPartnerNameSnapshot ?? string.Empty, source.BusinessPartnerAddressSnapshot,
            source.BusinessPartnerTaxOfficeSnapshot, source.BusinessPartnerTaxNumberSnapshot);
    }
}
