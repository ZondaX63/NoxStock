namespace NoxStock.Products;

public class ProductInventorySummaryDto
{
    public long TotalCount { get; set; }
    public long DynamicPricingCount { get; set; }
    public long MissingCostCount { get; set; }
    public long AttentionStockCount { get; set; }
}
