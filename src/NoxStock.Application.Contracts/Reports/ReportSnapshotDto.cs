using System;
using System.Collections.Generic;
using NoxStock.Accounts;
using NoxStock.BusinessPartners;
using NoxStock.StockMovements;

namespace NoxStock.Reports;

public class ReportRequestDto
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int PageSize { get; set; } = 25;
    public int AccountsPage { get; set; } = 1;
    public int BusinessPartnersPage { get; set; } = 1;
    public int ProductProfitabilityPage { get; set; } = 1;
    public int StocksPage { get; set; } = 1;
    public int StockMovementsPage { get; set; } = 1;
}

public class ReportSnapshotDto
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public ReportSummaryDto Summary { get; set; } = new();
    public List<StockReportItemDto> Stocks { get; set; } = new();
    public List<StockMovementReportItemDto> StockMovements { get; set; } = new();
    public List<AccountReportItemDto> Accounts { get; set; } = new();
    public List<BusinessPartnerReportItemDto> BusinessPartners { get; set; } = new();
    public List<VatReportItemDto> VatRates { get; set; } = new();
    public List<AgingReportItemDto> Aging { get; set; } = new();
    public List<ProductProfitabilityReportItemDto> ProductProfitability { get; set; } = new();
    public ReportPageInfoDto AccountsPaging { get; set; } = new();
    public ReportPageInfoDto BusinessPartnersPaging { get; set; } = new();
    public ReportPageInfoDto ProductProfitabilityPaging { get; set; } = new();
    public ReportPageInfoDto StocksPaging { get; set; } = new();
    public ReportPageInfoDto StockMovementsPaging { get; set; } = new();
}

public class ReportPageInfoDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public bool HasPrevious => PageNumber > 1;
    public bool HasNext { get; set; }
}

public class ReportSummaryDto
{
    public decimal Sales { get; set; }
    public decimal Purchases { get; set; }
    public decimal GrossProfitEstimate { get; set; }
    public decimal CashIn { get; set; }
    public decimal CashOut { get; set; }
    public decimal NetCashFlow { get; set; }
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public decimal Receivables { get; set; }
    public decimal Payables { get; set; }
    public decimal InventoryCostValue { get; set; }
    public int CriticalStockCount { get; set; }
}

public class StockReportItemDto
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal CriticalStockLevel { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal CostValue { get; set; }
    public bool IsCritical { get; set; }
}

public class StockMovementReportItemDto
{
    public DateTime Date { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public StockMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Description { get; set; }
}

public class AccountReportItemDto
{
    public Guid AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public string Currency { get; set; } = "TRY";
    public decimal Balance { get; set; }
    public decimal Inflow { get; set; }
    public decimal Outflow { get; set; }
    public decimal NetFlow { get; set; }
}

public class BusinessPartnerReportItemDto
{
    public Guid BusinessPartnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public BusinessPartnerType Type { get; set; }
    public string Currency { get; set; } = "TRY";
    public decimal Balance { get; set; }
    public decimal Sales { get; set; }
    public decimal Purchases { get; set; }
}

public class VatReportItemDto
{
    public decimal VatRate { get; set; }
    public decimal SalesVat { get; set; }
    public decimal PurchaseVat { get; set; }
    public decimal NetVat { get; set; }
}

public class AgingReportItemDto
{
    public string Bucket { get; set; } = string.Empty;
    public decimal Receivables { get; set; }
    public decimal Payables { get; set; }
}

public class ProductProfitabilityReportItemDto
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal SoldQuantity { get; set; }
    public decimal SalesAmount { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal GrossProfitEstimate { get; set; }
    public decimal GrossMarginPercent { get; set; }
}
