using System;
using System.Collections.Generic;

namespace NoxStock.CustomerSalesHistory;

public class CustomerSalesHistoryDto
{
    public CustomerSalesHistoryHeaderDto Customer { get; set; } = new();
    public CustomerSalesHistorySummaryDto Summary { get; set; } = new();
    public List<CustomerSalesHistoryGroupDto> Groups { get; set; } = new();
    public List<CustomerSalesHistoryChartPointDto> Chart { get; set; } = new();
    public CustomerSalesHistoryPeriodPreset PeriodPreset { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public long TotalCount { get; set; }
    public int SkipCount { get; set; }
    public int MaxResultCount { get; set; }
    public bool HasNextPage { get; set; }
}

public class CustomerSalesHistoryHeaderDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public decimal Balance { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal AvailableCredit { get; set; }
    public string Currency { get; set; } = "TRY";
}

public class CustomerSalesHistorySummaryDto
{
    public List<CustomerSalesHistoryCurrencyAmountDto> NetSales { get; set; } = new();
    public decimal TotalQuantity { get; set; }
    public long DocumentCount { get; set; }
    public List<CustomerSalesHistoryCurrencyAmountDto> AverageDocumentAmount { get; set; } = new();
    public string? TopProductName { get; set; }
    public string? TopProductSku { get; set; }
    public decimal TopProductQuantity { get; set; }
}

public class CustomerSalesHistoryCurrencyAmountDto
{
    public string Currency { get; set; } = "TRY";
    public decimal Amount { get; set; }
}

public class CustomerSalesHistoryGroupDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Quantity { get; set; }
    public long TransactionCount { get; set; }
    public List<CustomerSalesHistoryCurrencyAmountDto> Totals { get; set; } = new();
    public List<CustomerSalesHistoryRowDto> Rows { get; set; } = new();
}

public class CustomerSalesHistoryRowDto
{
    public Guid DocumentId { get; set; }
    public Guid DocumentItemId { get; set; }
    public DateTime Date { get; set; }
    public CustomerSalesHistoryDocumentType TransactionType { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Unit { get; set; } = string.Empty;
    public Guid? BrandId { get; set; }
    public string? BrandName { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal VatRate { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "TRY";
}

public class CustomerSalesHistoryChartPointDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<CustomerSalesHistoryCurrencyAmountDto> Totals { get; set; } = new();
}
