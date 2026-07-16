using System.Collections.Generic;

namespace NoxStock.Dashboard;

public class DashboardSummaryDto
{
    public decimal TodaySales { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public decimal TotalAccountBalance { get; set; }
    public decimal CustomerReceivables { get; set; }
    public decimal SupplierPayables { get; set; }
    public decimal NetPosition { get; set; }
    public int CriticalStockCount { get; set; }
    public int PendingInvoiceCount { get; set; }
    public int ActiveCustomerCount { get; set; }
    public List<DashboardDocumentSummaryDto> RecentDocuments { get; set; } = new();
    public List<DashboardCriticalStockDto> CriticalStocks { get; set; } = new();
    public List<DashboardSalesTrendDto> SalesTrend { get; set; } = new();
    public List<DashboardTopProductDto> TopProducts { get; set; } = new();
    public List<DashboardDueDocumentDto> DueDocuments { get; set; } = new();
}
