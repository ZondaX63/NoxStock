using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NexStock.Notifications;
using NexStock.Products;
using Shouldly;
using Xunit;

namespace NexStock.EntityFrameworkCore.Applications;

public class OperationalAlertsAppServiceTests : NexStockEntityFrameworkCoreTestBase
{
    private readonly IOperationalAlertsAppService _alertsAppService;
    private readonly IProductAppService _productAppService;

    public OperationalAlertsAppServiceTests()
    {
        _alertsAppService = ServiceProvider.GetRequiredService<IOperationalAlertsAppService>();
        _productAppService = ServiceProvider.GetRequiredService<IProductAppService>();
    }

    [Fact]
    public async Task Should_List_Critical_Stock_As_Operational_Alert()
    {
        var product = await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = "Critical Alert Product",
            Sku = "ALT-001",
            Barcode = "869000000202",
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = "A1",
            PurchasePrice = 10,
            SalePrice = 20,
            Quantity = 0,
            CriticalStockLevel = 3,
            TrackStock = true,
            Currency = "TRY",
            PurchaseCurrency = "TRY",
            SaleCurrency = "TRY"
        });

        var summary = await _alertsAppService.GetSummaryAsync();

        summary.TotalCount.ShouldBeGreaterThan(0);
        summary.CriticalCount.ShouldBeGreaterThan(0);
        summary.Alerts.Any(x => x.Type == "CriticalStock" && x.Title == "Stok tükendi" && x.Message.Contains(product.Name)).ShouldBeTrue();
    }
}
