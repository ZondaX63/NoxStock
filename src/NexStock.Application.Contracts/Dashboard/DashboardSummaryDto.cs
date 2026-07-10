using System.Collections.Generic;

namespace NexStock.Dashboard;

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
}

