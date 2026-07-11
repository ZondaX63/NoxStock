using System;

namespace NexStock.Dashboard;

public class DashboardTopProductDto
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Revenue { get; set; }
}
