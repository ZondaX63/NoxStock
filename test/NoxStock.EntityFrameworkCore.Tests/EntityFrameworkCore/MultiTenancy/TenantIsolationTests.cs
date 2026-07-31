using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.Accounts;
using NoxStock.BusinessPartners;
using NoxStock.Brands;
using NoxStock.Categories;
using NoxStock.Documents;
using NoxStock.Dashboard;
using NoxStock.ExchangeRates;
using NoxStock.Products;
using NoxStock.Reports;
using NoxStock.Sales;
using NoxStock.Settings;
using NoxStock.Tenants;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Xunit;

namespace NoxStock.EntityFrameworkCore.MultiTenancy;

[Collection(NoxStockTestConsts.CollectionDefinitionName)]
public class TenantIsolationTests : NoxStockEntityFrameworkCoreTestBase, IAsyncLifetime
{
    private readonly TwoTenantTestContext _tenants;
    private readonly IProductAppService _productAppService;
    private readonly IBrandAppService _brandAppService;
    private readonly ICategoryAppService _categoryAppService;
    private readonly IBusinessPartnerAppService _partnerAppService;
    private readonly IDocumentAppService _documentAppService;
    private readonly IDocumentWorkflowAppService _documentWorkflowAppService;
    private readonly ICommercialDocumentWorkflowAppService _commercialDocumentWorkflowAppService;
    private readonly IPosSaleAppService _posSaleAppService;
    private readonly IReportingAppService _reportingAppService;
    private readonly IDashboardAppService _dashboardAppService;
    private readonly ICompanySettingsAppService _companySettingsAppService;
    private readonly IExchangeRateAppService _exchangeRateAppService;
    private readonly ITenantRegistrationAppService _tenantRegistrationAppService;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<BusinessPartner, Guid> _partnerRepository;
    private readonly IRepository<ExchangeRate, Guid> _exchangeRateRepository;
    private readonly IRepository<NoxStock.Accounts.Account, Guid> _accountRepository;
    private readonly TenantDataSeederContributor _tenantDataSeeder;

    public TenantIsolationTests()
    {
        _tenants = new TwoTenantTestContext(
            ServiceProvider.GetRequiredService<ICurrentTenant>(),
            ServiceProvider.GetRequiredService<ITenantManager>(),
            ServiceProvider.GetRequiredService<ITenantRepository>(),
            ServiceProvider.GetRequiredService<IRepository<IdentityUser, Guid>>(),
            ServiceProvider.GetRequiredService<IGuidGenerator>());
        _productAppService = ServiceProvider.GetRequiredService<IProductAppService>();
        _brandAppService = ServiceProvider.GetRequiredService<IBrandAppService>();
        _categoryAppService = ServiceProvider.GetRequiredService<ICategoryAppService>();
        _partnerAppService = ServiceProvider.GetRequiredService<IBusinessPartnerAppService>();
        _documentAppService = ServiceProvider.GetRequiredService<IDocumentAppService>();
        _documentWorkflowAppService = ServiceProvider.GetRequiredService<IDocumentWorkflowAppService>();
        _commercialDocumentWorkflowAppService = ServiceProvider.GetRequiredService<ICommercialDocumentWorkflowAppService>();
        _posSaleAppService = ServiceProvider.GetRequiredService<IPosSaleAppService>();
        _reportingAppService = ServiceProvider.GetRequiredService<IReportingAppService>();
        _dashboardAppService = ServiceProvider.GetRequiredService<IDashboardAppService>();
        _companySettingsAppService = ServiceProvider.GetRequiredService<ICompanySettingsAppService>();
        _exchangeRateAppService = ServiceProvider.GetRequiredService<IExchangeRateAppService>();
        _tenantRegistrationAppService = ServiceProvider.GetRequiredService<ITenantRegistrationAppService>();
        _productRepository = ServiceProvider.GetRequiredService<IRepository<Product, Guid>>();
        _partnerRepository = ServiceProvider.GetRequiredService<IRepository<BusinessPartner, Guid>>();
        _exchangeRateRepository = ServiceProvider.GetRequiredService<IRepository<ExchangeRate, Guid>>();
        _accountRepository = ServiceProvider.GetRequiredService<IRepository<NoxStock.Accounts.Account, Guid>>();
        _tenantDataSeeder = ServiceProvider.GetRequiredService<TenantDataSeederContributor>();
    }

    public Task InitializeAsync() => _tenants.InitializeAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Tenant_A_Cannot_See_Or_Update_Tenant_B_Product()
    {
        ProductDto tenantBProduct;
        using (_tenants.ChangeToTenantB())
        {
            tenantBProduct = await CreateProductAsync("B-PRODUCT");
        }

        using (_tenants.ChangeToTenantA())
        {
            var list = await _productAppService.GetListAsync(new ProductGetListInput { MaxResultCount = 100 });
            list.Items.ShouldNotContain(x => x.Id == tenantBProduct.Id);
            await Should.ThrowAsync<EntityNotFoundException>(() =>
                _productAppService.UpdateAsync(tenantBProduct.Id, CreateProductInput("ILLEGAL-UPDATE")));
        }
    }

    [Fact]
    public async Task Tenant_A_Cannot_Read_Tenant_B_Document()
    {
        DocumentDto tenantBDocument;
        using (_tenants.ChangeToTenantB())
        {
            var product = await CreateProductAsync("B-DOC-PRODUCT");
            var partner = await CreatePartnerAsync("B-DOC-CUSTOMER", 0);
            tenantBDocument = await CreateDocumentAsync(partner.Id, product.Id, "B-DOC-001");
        }

        using (_tenants.ChangeToTenantA())
        {
            await Should.ThrowAsync<EntityNotFoundException>(() => _documentAppService.GetAsync(tenantBDocument.Id));
        }
    }

    [Fact]
    public async Task Tenant_A_Cannot_Record_Payment_For_Tenant_B_Invoice()
    {
        DocumentDto tenantBDocument;
        using (_tenants.ChangeToTenantB())
        {
            var product = await CreateProductAsync("B-PAYMENT-PRODUCT");
            var partner = await CreatePartnerAsync("B-PAYMENT-CUSTOMER", 0);
            tenantBDocument = await CreateDocumentAsync(partner.Id, product.Id, "B-PAYMENT-001");
            await _documentWorkflowAppService.ApproveAsync(tenantBDocument.Id, new ApproveDocumentInputDto());
        }

        using (_tenants.ChangeToTenantA())
        {
            await Should.ThrowAsync<EntityNotFoundException>(() =>
                _documentWorkflowAppService.RecordPaymentAsync(tenantBDocument.Id, new RecordDocumentPaymentInputDto
                {
                    OperationKey = "cross-tenant-payment",
                    AccountId = Guid.NewGuid(),
                    Amount = 1,
                    PaymentDate = DateTime.UtcNow
                }));
        }
    }

    [Fact]
    public async Task Tenant_A_Pos_Cannot_Change_Tenant_B_Stock()
    {
        ProductDto tenantBProduct;
        using (_tenants.ChangeToTenantB())
        {
            tenantBProduct = await CreateProductAsync("B-POS-PRODUCT", quantity: 10);
        }

        using (_tenants.ChangeToTenantA())
        {
            await Should.ThrowAsync<EntityNotFoundException>(() => _posSaleAppService.CreateAsync(new CreatePosSaleDto
            {
                PaymentMethod = PosPaymentMethod.Cash,
                Items = { new CreatePosSaleItemDto { ProductId = tenantBProduct.Id, Quantity = 1 } }
            }));
        }

        using (_tenants.ChangeToTenantB())
        {
            (await _productRepository.GetAsync(tenantBProduct.Id)).Quantity.ShouldBe(10);
        }
    }

    [Fact]
    public async Task Tenant_A_Report_Does_Not_Contain_Tenant_B_Balance()
    {
        using (_tenants.ChangeToTenantB())
        {
            await CreatePartnerAsync("B-REPORT-CUSTOMER", 9876);
        }

        using (_tenants.ChangeToTenantA())
        {
            var report = await _reportingAppService.GetSnapshotAsync(new ReportRequestDto());
            report.Summary.Receivables.ShouldBe(0);
            report.BusinessPartners.ShouldNotContain(x => x.Name == "B-REPORT-CUSTOMER");
        }
    }

    [Fact]
    public async Task Product_Input_Cannot_Override_Current_Tenant()
    {
        typeof(CreateUpdateProductDto).GetProperty("TenantId").ShouldBeNull();

        ProductDto product;
        using (_tenants.ChangeToTenantA())
        {
            product = await CreateProductAsync("A-TENANT-OWNED");
            (await _productRepository.GetAsync(product.Id)).TenantId.ShouldBe(_tenants.TenantA.Id);
        }
    }

    [Fact]
    public async Task Host_Context_Cannot_Create_Commercial_Product()
    {
        using (_tenants.ChangeToHost())
        {
            var exception = await Should.ThrowAsync<BusinessException>(() =>
                _productAppService.CreateAsync(CreateProductInput("HOST-PRODUCT")));
            exception.Code.ShouldBe("NoxStock:TenantRequired");
        }
    }

    [Fact]
    public async Task Host_Context_Cannot_Use_Commercial_Read_Or_Workflow_Endpoints()
    {
        using (_tenants.ChangeToHost())
        {
            await AssertTenantRequiredAsync(() => _productAppService.GetListAsync(new ProductGetListInput()));
            await AssertTenantRequiredAsync(() => _documentAppService.GetListAsync(new DocumentGetListInput()));
            await AssertTenantRequiredAsync(() => _posSaleAppService.GetCheckoutOptionsAsync());
            await AssertTenantRequiredAsync(() => _posSaleAppService.GetCustomersAsync(new PosCustomerLookupInputDto()));
            await AssertTenantRequiredAsync(() => _reportingAppService.GetSnapshotAsync(new ReportRequestDto()));
            await AssertTenantRequiredAsync(() => _dashboardAppService.GetSummaryAsync());
            await AssertTenantRequiredAsync(() => _companySettingsAppService.GetAsync());
        }
    }

    [Fact]
    public async Task Changing_Tenant_Context_Hides_Previous_Tenant_Data()
    {
        ProductDto tenantAProduct;
        using (_tenants.ChangeToTenantA())
        {
            tenantAProduct = await CreateProductAsync("A-SWITCH-PRODUCT");
        }

        using (_tenants.ChangeToTenantB())
        {
            var list = await _productAppService.GetListAsync(new ProductGetListInput { MaxResultCount = 100 });
            list.Items.ShouldNotContain(x => x.Id == tenantAProduct.Id);
        }
    }

    [Fact]
    public async Task Tenant_A_Cannot_Use_Tenant_B_Relation_Ids()
    {
        BrandDto tenantBBrand;
        CategoryDto tenantBCategory;
        BusinessPartnerDto tenantBPartner;
        ProductDto tenantBProduct;
        using (_tenants.ChangeToTenantB())
        {
            tenantBBrand = await _brandAppService.CreateAsync(new CreateUpdateBrandDto { Name = "B Brand" });
            tenantBCategory = await _categoryAppService.CreateAsync(new CreateUpdateCategoryDto { Name = "B Category" });
            tenantBPartner = await CreatePartnerAsync("B-RELATION-CUSTOMER", 0);
            tenantBProduct = await CreateProductAsync("B-RELATION-PRODUCT");
        }

        using (_tenants.ChangeToTenantA())
        {
            var productInput = CreateProductInput("A-ILLEGAL-BRAND");
            productInput.BrandId = tenantBBrand.Id;
            await AssertBusinessCodeAsync(
                () => _productAppService.CreateAsync(productInput),
                "NoxStock:InvalidBrandReference");

            await AssertBusinessCodeAsync(
                () => _categoryAppService.CreateAsync(new CreateUpdateCategoryDto
                {
                    Name = "A Illegal Child",
                    ParentId = tenantBCategory.Id
                }),
                "NoxStock:InvalidParentCategoryReference");

            var tenantAProduct = await CreateProductAsync("A-RELATION-PRODUCT");
            var tenantAPartner = await CreatePartnerAsync(
                "A-RELATION-SUPPLIER",
                0,
                BusinessPartnerType.Supplier);
            await AssertBusinessCodeAsync(
                () => CreateDocumentAsync(tenantBPartner.Id, tenantAProduct.Id, "A-ILLEGAL-PARTNER"),
                "NoxStock:InvalidBusinessPartnerReference");

            await AssertBusinessCodeAsync(
                () => CreatePurchaseDocumentAsync(tenantAPartner.Id, tenantBProduct.Id),
                "NoxStock:InvalidProductReference");
        }
    }

    [Fact]
    public async Task Tenant_A_Pos_Cannot_Use_Tenant_B_Payment_Account()
    {
        NoxStock.Accounts.Account tenantBAccount;
        using (_tenants.ChangeToTenantB())
        {
            tenantBAccount = new NoxStock.Accounts.Account(
                Guid.NewGuid(),
                _tenants.TenantB.Id,
                "Tenant B Cash",
                AccountType.Cash);
            await _accountRepository.InsertAsync(tenantBAccount, autoSave: true);
        }

        using (_tenants.ChangeToTenantA())
        {
            var tenantAProduct = await CreateProductAsync("A-ACCOUNT-ISOLATION", quantity: 4);

            await Should.ThrowAsync<EntityNotFoundException>(() =>
                _posSaleAppService.CreateAsync(new CreatePosSaleDto
                {
                    PaymentMethod = PosPaymentMethod.Cash,
                    AccountId = tenantBAccount.Id,
                    Items =
                    {
                        new CreatePosSaleItemDto { ProductId = tenantAProduct.Id, Quantity = 1 }
                    }
                }));

            (await _productRepository.GetAsync(tenantAProduct.Id)).Quantity.ShouldBe(4);
        }
    }

    [Fact]
    public async Task Tenant_A_Cannot_Convert_Tenant_B_Document()
    {
        DocumentDto tenantBOffer;
        using (_tenants.ChangeToTenantB())
        {
            var product = await CreateProductAsync("B-CONVERSION-PRODUCT");
            var partner = await CreatePartnerAsync("B-CONVERSION-CUSTOMER", 0);
            tenantBOffer = await CreateDocumentAsync(
                partner.Id,
                product.Id,
                "B-OFFER-001",
                DocumentType.Offer);
            await _commercialDocumentWorkflowAppService.SendOfferAsync(tenantBOffer.Id);
        }

        using (_tenants.ChangeToTenantA())
        {
            await Should.ThrowAsync<EntityNotFoundException>(() =>
                _commercialDocumentWorkflowAppService.ConvertOfferToInvoiceAsync(tenantBOffer.Id));
        }
    }

    [Fact]
    public async Task Exchange_Rates_Should_Be_Global_While_Company_Settings_Are_Tenant_Scoped()
    {
        using (_tenants.ChangeToHost())
        {
            await _exchangeRateRepository.InsertAsync(
                new ExchangeRate(Guid.NewGuid(), "USD", 42.50m, DateTime.UtcNow),
                autoSave: true);
        }

        using (_tenants.ChangeToTenantA())
        {
            await _companySettingsAppService.UpdateAsync(CreateCompanySettings("Tenant A Ltd.", "USD"));
            var rates = await _exchangeRateAppService.GetCurrentAsync();
            rates.USD.ShouldNotBeNull();
            rates.USD!.Rate.ShouldBe(42.50m);
        }

        using (_tenants.ChangeToTenantB())
        {
            await _companySettingsAppService.UpdateAsync(CreateCompanySettings("Tenant B Ltd.", "EUR"));
            (await _companySettingsAppService.GetAsync()).CompanyName.ShouldBe("Tenant B Ltd.");
            (await _exchangeRateAppService.GetCurrentAsync()).USD!.Rate.ShouldBe(42.50m);
        }

        using (_tenants.ChangeToTenantA())
        {
            var settings = await _companySettingsAppService.GetAsync();
            settings.CompanyName.ShouldBe("Tenant A Ltd.");
            settings.DefaultCurrency.ShouldBe("USD");
        }

        using (_tenants.ChangeToHost())
        {
            await AssertTenantRequiredAsync(() =>
                _companySettingsAppService.UpdateAsync(CreateCompanySettings("Host", "TRY")));
        }
    }

    [Fact]
    public async Task Public_Tenant_Registration_Should_Be_Closed_By_Default()
    {
        using (_tenants.ChangeToHost())
        {
            var exception = await Should.ThrowAsync<BusinessException>(() =>
                _tenantRegistrationAppService.RegisterTenantAsync(new RegisterTenantDto
                {
                    TenantName = "public-registration-attempt",
                    AdminEmailAddress = "admin@registration.test",
                    AdminPassword = "StrongPassword123!"
                }));

            exception.Code.ShouldBe("NoxStock:PublicRegistrationDisabled");
        }
    }

    [Fact]
    public async Task Tenant_Default_Seed_Should_Be_Idempotent_And_Isolated()
    {
        using (_tenants.ChangeToTenantA())
        {
            await _tenantDataSeeder.SeedAsync(new DataSeedContext(_tenants.TenantA.Id));
            await _tenantDataSeeder.SeedAsync(new DataSeedContext(_tenants.TenantA.Id));

            var accounts = await _accountRepository.GetListAsync();
            accounts.Count.ShouldBe(2);
            accounts.ShouldContain(x => x.Name == "Merkez Kasa");
            accounts.ShouldContain(x => x.Name == "Vadesiz TL Hesabı");
        }

        using (_tenants.ChangeToTenantB())
        {
            await _tenantDataSeeder.SeedAsync(new DataSeedContext(_tenants.TenantB.Id));
            (await _accountRepository.GetCountAsync()).ShouldBe(2);
        }
    }

    private Task<ProductDto> CreateProductAsync(string sku, decimal quantity = 5)
    {
        return _productAppService.CreateAsync(CreateProductInput(sku, quantity));
    }

    private static async Task AssertTenantRequiredAsync(Func<Task> action)
    {
        var exception = await Should.ThrowAsync<BusinessException>(action);
        exception.Code.ShouldBe("NoxStock:TenantRequired");
    }

    private static async Task AssertBusinessCodeAsync(Func<Task> action, string expectedCode)
    {
        var exception = await Should.ThrowAsync<BusinessException>(action);
        exception.Code.ShouldBe(expectedCode);
    }

    private static CreateUpdateProductDto CreateProductInput(string sku, decimal quantity = 5)
    {
        return new CreateUpdateProductDto
        {
            Name = sku,
            Sku = sku,
            Barcode = string.Empty,
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = string.Empty,
            PurchasePrice = 10,
            SalePrice = 20,
            Quantity = quantity,
            TrackStock = true,
            PurchaseCurrency = "TRY",
        };
    }

    private static CompanySettingsDto CreateCompanySettings(string companyName, string currency)
    {
        return new CompanySettingsDto
        {
            CompanyName = companyName,
            TaxOffice = "Kadikoy",
            TaxNumber = "1234567890",
            Address = "Istanbul",
            Phone = "5550000000",
            Email = "tenant@noxstock.test",
            DefaultCurrency = currency,
            DefaultVatRate = 20,
            DefaultUnit = "Adet",
            SaleInvoiceNumberPrefix = "SF",
            PurchaseInvoiceNumberPrefix = "AF",
            OfferNumberPrefix = "TKL",
            SaleOrderNumberPrefix = "SS",
            PurchaseOrderNumberPrefix = "AS",
            PosSaleNumberPrefix = "POS",
            DefaultLabelFormat = "barcode"
        };
    }

    private async Task<BusinessPartnerDto> CreatePartnerAsync(
        string name,
        decimal balance,
        BusinessPartnerType type = BusinessPartnerType.Customer)
    {
        var dto = await _partnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = name,
            Type = type,
            Currency = "TRY"
        });
        var entity = await _partnerRepository.GetAsync(dto.Id);
        entity.Balance = balance;
        await _partnerRepository.UpdateAsync(entity, autoSave: true);
        return dto;
    }

    private Task<DocumentDto> CreateDocumentAsync(
        Guid partnerId,
        Guid productId,
        string number,
        DocumentType type = DocumentType.SaleInvoice)
    {
        return _documentAppService.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = partnerId,
            DocNumber = number,
            Type = type,
            Status = DocumentStatus.Draft,
            Currency = "TRY",
            Date = DateTime.UtcNow,
            Items =
            {
                new CreateUpdateDocumentItemDto
                {
                    ProductId = productId,
                    Quantity = 1,
                    UnitPrice = 20,
                    VatRate = 0
                }
            }
        });
    }

    private Task<DocumentDto> CreatePurchaseDocumentAsync(Guid partnerId, Guid productId)
    {
        return _documentAppService.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = partnerId,
            DocNumber = "A-ILLEGAL-PURCHASE-PRODUCT",
            Type = DocumentType.PurchaseInvoice,
            Status = DocumentStatus.Draft,
            Currency = "TRY",
            Date = DateTime.UtcNow,
            Items =
            {
                new CreateUpdateDocumentItemDto
                {
                    ProductId = productId,
                    Quantity = 1,
                    UnitPrice = 10,
                    VatRate = 20
                }
            }
        });
    }
}
