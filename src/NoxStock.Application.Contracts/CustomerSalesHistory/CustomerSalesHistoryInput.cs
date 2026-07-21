using System;
using Volo.Abp.Application.Dtos;

namespace NoxStock.CustomerSalesHistory;

public class CustomerSalesHistoryInput : PagedAndSortedResultRequestDto
{
    public Guid CustomerId { get; set; }
    public bool IncludePurchases { get; set; }
    public CustomerSalesHistoryPeriodPreset PeriodPreset { get; set; } = CustomerSalesHistoryPeriodPreset.ThisMonth;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public CustomerSalesHistoryDocumentType DocumentType { get; set; }
    public string? ProductSearch { get; set; }
    public string? DocumentNumber { get; set; }
    public Guid? BrandId { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Currency { get; set; }
    public bool OnlyReturns { get; set; }
}
