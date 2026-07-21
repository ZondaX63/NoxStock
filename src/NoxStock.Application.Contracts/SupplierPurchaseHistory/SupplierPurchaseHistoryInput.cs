using System;
using Volo.Abp.Application.Dtos;
using NoxStock.CustomerSalesHistory;

namespace NoxStock.SupplierPurchaseHistory;

public class SupplierPurchaseHistoryInput : PagedAndSortedResultRequestDto
{
    public Guid SupplierId { get; set; }
    public CustomerSalesHistoryPeriodPreset PeriodPreset { get; set; } = CustomerSalesHistoryPeriodPreset.ThisMonth;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public CustomerSalesHistoryDocumentType DocumentType { get; set; }
    public string? ProductSearch { get; set; }
    public string? DocumentNumber { get; set; }
    public Guid? BrandId { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Currency { get; set; }
}
