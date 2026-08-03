using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MiniExcelLibs;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.Products;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace NoxStock.EntityFrameworkCore.Applications;

public class ProductImportAppServiceTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly IProductAppService _productAppService;
    private readonly IRepository<Product, System.Guid> _productRepository;

    public ProductImportAppServiceTests()
    {
        _productAppService = ServiceProvider.GetRequiredService<IProductAppService>();
        _productRepository = ServiceProvider.GetRequiredService<IRepository<Product, System.Guid>>();
    }

    [Fact]
    public async Task Should_Create_Product_With_Only_Name_And_Generate_Sku()
    {
        var product = await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = "Minimum alanlı ürün",
            Sku = string.Empty
        });

        product.Name.ShouldBe("Minimum alanlı ürün");
        product.Sku.ShouldStartWith("STK-");
        product.Unit.ShouldBe("Adet");
    }

    [Fact]
    public async Task Should_Import_Unique_Stock_Rows_And_Ignore_Broken_Barcode()
    {
        using var stream = new MemoryStream();
        var rows = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["Ürün Adı"] = "Fren balatası",
                ["SKU"] = "STOCK-001",
                ["Barkod"] = "869000000001",
                ["Marka"] = "Test Marka",
                ["Kategori"] = "Test Kategori",
                ["Raf"] = "A-01",
                ["Miktar"] = 12.5m,
                ["Birim"] = "Adet",
                ["Açıklama"] = null,
                ["Etiketler"] = "OEM-1, OEM-2",
                ["Kritik Stok Miktarı"] = 2m
            },
            new()
            {
                ["Ürün Adı"] = "Yinelenen satır",
                ["SKU"] = "STOCK-001",
                ["Barkod"] = null,
                ["Marka"] = null,
                ["Kategori"] = null,
                ["Raf"] = null,
                ["Miktar"] = 999m,
                ["Birim"] = null,
                ["Açıklama"] = null,
                ["Etiketler"] = null,
                ["Kritik Stok Miktarı"] = null
            },
            new()
            {
                ["Ürün Adı"] = "Yağ filtresi",
                ["SKU"] = "STOCK-002",
                ["Barkod"] = "8,1E+12",
                ["Marka"] = null,
                ["Kategori"] = null,
                ["Raf"] = null,
                ["Miktar"] = 4m,
                ["Birim"] = "Adet",
                ["Açıklama"] = null,
                ["Etiketler"] = null,
                ["Kritik Stok Miktarı"] = null
            },
            new()
            {
                ["Ürün Adı"] = "Ondalık stok miktarı",
                ["SKU"] = "STOCK-003",
                ["Barkod"] = null,
                ["Marka"] = null,
                ["Kategori"] = null,
                ["Raf"] = null,
                ["Miktar"] = "1.0",
                ["Birim"] = "Adet",
                ["Açıklama"] = null,
                ["Etiketler"] = null,
                ["Kritik Stok Miktarı"] = "1,5"
            }
        };
        stream.SaveAs(rows);

        var result = await _productAppService.ImportAsync(new ProductImportInputDto
        {
            FileName = "stok-sayimi.xlsx",
            Content = stream.ToArray()
        });

        result.CreatedCount.ShouldBe(3);
        result.DuplicateRowCount.ShouldBe(1);
        result.IgnoredScientificBarcodeCount.ShouldBe(1);
        result.CreatedBrandCount.ShouldBe(1);
        result.CreatedCategoryCount.ShouldBe(1);

        var products = await _productRepository.GetListAsync(product =>
            product.Sku == "STOCK-001" || product.Sku == "STOCK-002" || product.Sku == "STOCK-003");
        products.Count.ShouldBe(3);
        products.Single(product => product.Sku == "STOCK-001").Quantity.ShouldBe(12.5m);
        products.Single(product => product.Sku == "STOCK-002").Barcode.ShouldBe(string.Empty);
        products.Single(product => product.Sku == "STOCK-003").Quantity.ShouldBe(1m);
        products.Single(product => product.Sku == "STOCK-003").CriticalStockLevel.ShouldBe(1.5m);
    }

}
