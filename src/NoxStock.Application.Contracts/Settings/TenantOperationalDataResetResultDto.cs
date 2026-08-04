namespace NoxStock.Settings;

public class TenantOperationalDataResetResultDto
{
    public int ProductCount { get; set; }
    public int ProductOemCodeCount { get; set; }
    public int ProductCostHistoryCount { get; set; }
    public int BrandCount { get; set; }
    public int CategoryCount { get; set; }
    public int BusinessPartnerCount { get; set; }
    public int AccountCount { get; set; }
    public int DocumentCount { get; set; }
    public int DocumentItemCount { get; set; }
    public int DocumentExchangeRateSnapshotCount { get; set; }
    public int StockMovementCount { get; set; }
    public int AccountTransactionCount { get; set; }
    public int InventoryCountCount { get; set; }
    public int InventoryCountItemCount { get; set; }

    public int TotalCount => ProductCount + ProductOemCodeCount + ProductCostHistoryCount + BrandCount +
                             CategoryCount + BusinessPartnerCount + AccountCount + DocumentCount +
                             DocumentItemCount + DocumentExchangeRateSnapshotCount + StockMovementCount +
                             AccountTransactionCount + InventoryCountCount + InventoryCountItemCount;
}
