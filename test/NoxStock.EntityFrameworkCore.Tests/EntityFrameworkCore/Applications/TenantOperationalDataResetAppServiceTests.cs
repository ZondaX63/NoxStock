using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.Accounts;
using NoxStock.Brands;
using NoxStock.BusinessPartners;
using NoxStock.Categories;
using NoxStock.Documents;
using NoxStock.Products;
using NoxStock.Settings;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace NoxStock.EntityFrameworkCore.Applications;

public class TenantOperationalDataResetAppServiceTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly ITenantOperationalDataResetAppService _resetAppService;
    private readonly IBrandAppService _brandAppService;
    private readonly ICategoryAppService _categoryAppService;
    private readonly IProductAppService _productAppService;
    private readonly IBusinessPartnerAppService _businessPartnerAppService;
    private readonly IAccountAppService _accountAppService;
    private readonly IDocumentAppService _documentAppService;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<Brand, Guid> _brandRepository;
    private readonly IRepository<Category, Guid> _categoryRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<DocumentItem, Guid> _documentItemRepository;

    public TenantOperationalDataResetAppServiceTests()
    {
        _resetAppService = ServiceProvider.GetRequiredService<ITenantOperationalDataResetAppService>();
        _brandAppService = ServiceProvider.GetRequiredService<IBrandAppService>();
        _categoryAppService = ServiceProvider.GetRequiredService<ICategoryAppService>();
        _productAppService = ServiceProvider.GetRequiredService<IProductAppService>();
        _businessPartnerAppService = ServiceProvider.GetRequiredService<IBusinessPartnerAppService>();
        _accountAppService = ServiceProvider.GetRequiredService<IAccountAppService>();
        _documentAppService = ServiceProvider.GetRequiredService<IDocumentAppService>();
        _productRepository = ServiceProvider.GetRequiredService<IRepository<Product, Guid>>();
        _brandRepository = ServiceProvider.GetRequiredService<IRepository<Brand, Guid>>();
        _categoryRepository = ServiceProvider.GetRequiredService<IRepository<Category, Guid>>();
        _businessPartnerRepository = ServiceProvider.GetRequiredService<IRepository<BusinessPartner, Guid>>();
        _accountRepository = ServiceProvider.GetRequiredService<IRepository<Account, Guid>>();
        _documentRepository = ServiceProvider.GetRequiredService<IRepository<Document, Guid>>();
        _documentItemRepository = ServiceProvider.GetRequiredService<IRepository<DocumentItem, Guid>>();
    }

    [Fact]
    public async Task Should_Reset_Only_Tenant_Operational_Data()
    {
        var brand = await _brandAppService.CreateAsync(new CreateUpdateBrandDto { Name = "Reset marka" });
        var category = await _categoryAppService.CreateAsync(new CreateUpdateCategoryDto { Name = "Reset kategori" });
        var product = await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = "Reset ürün",
            Sku = "RESET-PRODUCT",
            BrandId = brand.Id,
            CategoryId = category.Id,
            SalePrice = 100m
        });
        var customer = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = "Reset müşteri",
            Type = BusinessPartnerType.Customer
        });
        await _accountAppService.CreateAsync(new CreateUpdateAccountDto { Name = "Reset kasa" });
        await _documentAppService.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = customer.Id,
            DocNumber = "RESET-SALE-001",
            Type = DocumentType.SaleInvoice,
            Date = DateTime.UtcNow,
            Items =
            {
                new CreateUpdateDocumentItemDto
                {
                    ProductId = product.Id,
                    Quantity = 1m,
                    UnitPrice = 100m,
                    VatRate = 20m
                }
            }
        });

        var result = await _resetAppService.ResetAsync(new TenantOperationalDataResetInputDto
        {
            Confirmation = "SIFIRLA"
        });

        result.ProductCount.ShouldBe(1);
        result.BrandCount.ShouldBe(1);
        result.CategoryCount.ShouldBe(1);
        result.BusinessPartnerCount.ShouldBe(1);
        result.AccountCount.ShouldBe(1);
        result.DocumentCount.ShouldBe(1);
        result.DocumentItemCount.ShouldBe(1);
        (await _productRepository.GetCountAsync()).ShouldBe(0);
        (await _brandRepository.GetCountAsync()).ShouldBe(0);
        (await _categoryRepository.GetCountAsync()).ShouldBe(0);
        (await _businessPartnerRepository.GetCountAsync()).ShouldBe(0);
        (await _accountRepository.GetCountAsync()).ShouldBe(0);
        (await _documentRepository.GetCountAsync()).ShouldBe(0);
        (await _documentItemRepository.GetCountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Should_Require_Explicit_Reset_Confirmation()
    {
        await Should.ThrowAsync<BusinessException>(() => _resetAppService.ResetAsync(
            new TenantOperationalDataResetInputDto { Confirmation = "SİL" }));
    }
}
