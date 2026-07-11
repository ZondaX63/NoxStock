using System;

namespace NexStock.Dashboard;

public class DashboardCriticalStockDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal CriticalStockLevel { get; set; }
}
