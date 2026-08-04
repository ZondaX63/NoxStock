using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.InventoryCounts;
using NoxStock.Products;
using NoxStock.StockMovements;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace NoxStock.EntityFrameworkCore.Applications;

public class InventoryCountAppServiceTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly IInventoryCountAppService _countAppService;
    private readonly IProductAppService _productAppService;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<StockMovement, Guid> _movementRepository;

    public InventoryCountAppServiceTests()
    {
        _countAppService = ServiceProvider.GetRequiredService<IInventoryCountAppService>();
        _productAppService = ServiceProvider.GetRequiredService<IProductAppService>();
        _productRepository = ServiceProvider.GetRequiredService<IRepository<Product, Guid>>();
        _movementRepository = ServiceProvider.GetRequiredService<IRepository<StockMovement, Guid>>();
    }

    [Fact]
    public async Task Should_Atomically_Apply_Counted_Quantities_And_Zero_Unlisted_Products()
    {
        var counted = await CreateProductAsync("COUNT-001", 10m);
        var unchanged = await CreateProductAsync("COUNT-002", 4m);
        var omitted = await CreateProductAsync("COUNT-003", 9m);
        var draft = await _countAppService.GetOrCreateDraftAsync();

        draft = await _countAppService.SaveDraftAsync(new SaveInventoryCountDto
        {
            Id = draft.Id,
            Name = "Genel depo sayımı",
            Items =
            {
                new SaveInventoryCountItemDto { ProductId = counted.Id, CountedQuantity = 7m },
                new SaveInventoryCountItemDto { ProductId = unchanged.Id, CountedQuantity = 4m }
            }
        });

        draft.ProductsThatWillBeZeroed.ShouldBeGreaterThanOrEqualTo(1);
        var completed = await _countAppService.CompleteAsync(new CompleteInventoryCountDto
        {
            Id = draft.Id,
            ConfirmReset = true
        });

        completed.Status.ShouldBe(InventoryCountStatus.Completed);
        completed.ZeroedProductCount.ShouldBeGreaterThanOrEqualTo(1);
        (await _productRepository.GetAsync(counted.Id)).Quantity.ShouldBe(7m);
        (await _productRepository.GetAsync(unchanged.Id)).Quantity.ShouldBe(4m);
        (await _productRepository.GetAsync(omitted.Id)).Quantity.ShouldBe(0m);

        var movements = await _movementRepository.GetListAsync(x =>
            x.Type == StockMovementType.InventoryCount &&
            (x.ProductId == counted.Id || x.ProductId == omitted.Id));
        movements.Count.ShouldBe(2);
        movements.Single(x => x.ProductId == counted.Id).Quantity.ShouldBe(-3m);
        movements.Single(x => x.ProductId == counted.Id).BalanceAfter.ShouldBe(7m);
        movements.Single(x => x.ProductId == omitted.Id).Quantity.ShouldBe(-9m);
        movements.Single(x => x.ProductId == omitted.Id).BalanceAfter.ShouldBe(0m);

        await _countAppService.CompleteAsync(new CompleteInventoryCountDto
        {
            Id = draft.Id,
            ConfirmReset = true
        });
        (await _movementRepository.GetListAsync(x =>
            x.Type == StockMovementType.InventoryCount &&
            (x.ProductId == counted.Id || x.ProductId == omitted.Id))).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Should_Require_Explicit_Confirmation()
    {
        var product = await CreateProductAsync("COUNT-CONFIRM", 2m);
        var draft = await _countAppService.GetOrCreateDraftAsync();
        await _countAppService.SaveDraftAsync(new SaveInventoryCountDto
        {
            Id = draft.Id,
            Name = "Onay testi",
            Items = { new SaveInventoryCountItemDto { ProductId = product.Id, CountedQuantity = 2m } }
        });

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _countAppService.CompleteAsync(new CompleteInventoryCountDto { Id = draft.Id }));
        exception.Code.ShouldBe("NoxStock:InventoryCountConfirmationRequired");
    }

    [Fact]
    public async Task Should_Create_Quick_Product_As_Incomplete_Without_Changing_Stock_Before_Completion()
    {
        var result = await _countAppService.QuickCreateProductAsync(new QuickCreateInventoryProductDto
        {
            Name = "Sayımda bulunan ürün",
            Barcode = "COUNT-NEW-BARCODE",
            CountedQuantity = 5m,
            Note = "Etiketsiz kutu"
        });

        result.IsQuickCreatedProduct.ShouldBeTrue();
        result.CountedQuantity.ShouldBe(5m);
        var product = await _productRepository.GetAsync(result.ProductId);
        product.IsIncompleteProfile.ShouldBeTrue();
        product.TrackStock.ShouldBeTrue();
        product.Quantity.ShouldBe(0m);
        product.Sku.ShouldStartWith("SYM-");
    }

    [Fact]
    public async Task Should_Accumulate_Duplicate_Import_Rows_Without_Changing_Stock()
    {
        var product = await CreateProductAsync("COUNT-IMPORT", 8m);
        var csv = "SKU,Barkod,Miktar\nCOUNT-IMPORT,,1.0\nCOUNT-IMPORT,,2\n";

        var result = await _countAppService.ImportAsync(new InventoryCountImportInputDto
        {
            FileName = "sayim.csv",
            Content = Encoding.UTF8.GetBytes(csv)
        });

        result.SourceRowCount.ShouldBe(2);
        result.MatchedProductCount.ShouldBe(1);
        result.SkippedRowCount.ShouldBe(0);
        result.Items.Single().CountedQuantity.ShouldBe(3m);
        (await _productRepository.GetAsync(product.Id)).Quantity.ShouldBe(8m);
    }

    private Task<ProductDto> CreateProductAsync(string sku, decimal quantity)
    {
        return _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = sku,
            Sku = sku,
            Unit = "Adet",
            Quantity = quantity,
            TrackStock = true,
            PurchaseCurrency = "EUR"
        });
    }
}
