using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.Notifications;
using NoxStock.Products;
using Shouldly;
using Xunit;

namespace NoxStock.EntityFrameworkCore.Applications;

public class OperationalAlertsAppServiceTests : NoxStockEntityFrameworkCoreTestBase
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
            PurchaseCurrency = "TRY",
        });

        var summary = await _alertsAppService.GetSummaryAsync();

        summary.TotalCount.ShouldBeGreaterThan(0);
        summary.CriticalCount.ShouldBeGreaterThan(0);
        summary.Alerts.Any(x => x.Type == "CriticalStock" && x.Title == "Stok tükendi" && x.Message.Contains(product.Name)).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Keep_Summary_Small_And_Page_Operational_Alerts()
    {
        var suffix = System.Guid.NewGuid().ToString("N")[..8];
        for (var index = 0; index < 25; index++)
        {
            await _productAppService.CreateAsync(new CreateUpdateProductDto
            {
                Name = $"Paged alert {suffix} {index:00}",
                Sku = $"ALT-{suffix}-{index:00}",
                Barcode = $"88{suffix}{index:00}",
                Description = string.Empty,
                Unit = "Adet",
                ShelfLocation = "A1",
                PurchaseCurrency = "TRY",
                SalePrice = 20,
                Quantity = 0,
                CriticalStockLevel = 3,
                TrackStock = true
            });
        }

        var summary = await _alertsAppService.GetSummaryAsync();
        var firstPage = await _alertsAppService.GetPageAsync(new OperationalAlertGetListInput
        {
            SkipCount = 0,
            MaxResultCount = 20
        });
        var secondPage = await _alertsAppService.GetPageAsync(new OperationalAlertGetListInput
        {
            SkipCount = 20,
            MaxResultCount = 20
        });

        summary.Alerts.Count.ShouldBeLessThanOrEqualTo(4);
        firstPage.Alerts.Count.ShouldBe(20);
        secondPage.Alerts.Count.ShouldBeGreaterThan(0);
        firstPage.TotalCount.ShouldBeGreaterThanOrEqualTo(25);
        firstPage.Alerts.Select(x => x.Message)
            .Intersect(secondPage.Alerts.Select(x => x.Message))
            .ShouldBeEmpty();
    }
}
