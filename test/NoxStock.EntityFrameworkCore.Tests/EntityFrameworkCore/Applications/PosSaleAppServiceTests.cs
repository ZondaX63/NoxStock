using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.Accounts;
using NoxStock.BusinessPartners;
using NoxStock.ExchangeRates;
using NoxStock.Products;
using NoxStock.Sales;
using NoxStock.StockMovements;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace NoxStock.EntityFrameworkCore.Applications;

public class PosSaleAppServiceTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly IPosSaleAppService _posSaleAppService;
    private readonly IProductAppService _productAppService;
    private readonly IAccountAppService _accountAppService;
    private readonly IBusinessPartnerAppService _businessPartnerAppService;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IRepository<AccountTransaction, Guid> _accountTransactionRepository;
    private readonly IRepository<StockMovement, Guid> _stockMovementRepository;

    public PosSaleAppServiceTests()
    {
        _posSaleAppService = ServiceProvider.GetRequiredService<IPosSaleAppService>();
        _productAppService = ServiceProvider.GetRequiredService<IProductAppService>();
        _accountAppService = ServiceProvider.GetRequiredService<IAccountAppService>();
        _businessPartnerAppService = ServiceProvider.GetRequiredService<IBusinessPartnerAppService>();
        _productRepository = ServiceProvider.GetRequiredService<IRepository<Product, Guid>>();
        _accountRepository = ServiceProvider.GetRequiredService<IRepository<Account, Guid>>();
        _businessPartnerRepository = ServiceProvider.GetRequiredService<IRepository<BusinessPartner, Guid>>();
        _accountTransactionRepository = ServiceProvider.GetRequiredService<IRepository<AccountTransaction, Guid>>();
        _stockMovementRepository = ServiceProvider.GetRequiredService<IRepository<StockMovement, Guid>>();
    }

    [Fact]
    public async Task Should_Create_And_Cancel_Cash_Pos_Sale_With_Stock_And_Account_Effects()
    {
        var product = await CreateProductAsync("POS-CASH-001", "869000000001", 5, 125);
        var cashAccount = await _accountAppService.CreateAsync(new CreateUpdateAccountDto
        {
            Name = "POS Cash",
            Type = AccountType.Cash,
            Balance = 50,
            IsActive = true
        });

        var lookup = await _posSaleAppService.GetProductsAsync(new PosProductLookupInputDto { Filter = product.Barcode });
        lookup.Single().Id.ShouldBe(product.Id);

        var saleInput = new CreatePosSaleDto
        {
            OperationKey = "pos-cash-001",
            PaymentMethod = PosPaymentMethod.Cash,
            AccountId = cashAccount.Id,
            Items = { new CreatePosSaleItemDto { ProductId = product.Id, Quantity = 2 } }
        };
        var sale = await _posSaleAppService.CreateAsync(saleInput);
        var replayedSale = await _posSaleAppService.CreateAsync(saleInput);

        sale.Status.ShouldBe(NoxStock.Documents.DocumentStatus.Paid);
        replayedSale.Id.ShouldBe(sale.Id);
        sale.GrandTotal.ShouldBe(300);
        sale.PaymentMethod.ShouldBe(PosPaymentMethod.Cash);

        var productAfterSale = await _productRepository.GetAsync(product.Id);
        var cashAfterSale = await _accountRepository.GetAsync(cashAccount.Id);
        var stockMovements = await _stockMovementRepository.GetListAsync(x => x.DocumentId == sale.Id);
        var accountTransactions = await _accountTransactionRepository.GetListAsync(x => x.DocumentId == sale.Id);
        var recentSales = await _posSaleAppService.GetRecentAsync();

        productAfterSale.Quantity.ShouldBe(3);
        cashAfterSale.Balance.ShouldBe(350);
        stockMovements.Single().Type.ShouldBe(StockMovementType.PosSale);
        stockMovements.Single().Quantity.ShouldBe(-2);
        stockMovements.Count.ShouldBe(1);
        accountTransactions.Single().Type.ShouldBe(AccountTransactionType.Collection);
        accountTransactions.Single().Amount.ShouldBe(300);
        accountTransactions.Count.ShouldBe(1);
        recentSales.Single(x => x.Id == sale.Id).PaymentMethod.ShouldBe(PosPaymentMethod.Cash);

        var cancelled = await _posSaleAppService.CancelAsync(sale.Id);
        cancelled.Status.ShouldBe(NoxStock.Documents.DocumentStatus.Cancelled);

        var productAfterCancel = await _productRepository.GetAsync(product.Id);
        var cashAfterCancel = await _accountRepository.GetAsync(cashAccount.Id);
        var stockAfterCancel = await _stockMovementRepository.GetListAsync(x => x.DocumentId == sale.Id);
        var transactionsAfterCancel = await _accountTransactionRepository.GetListAsync(x => x.DocumentId == sale.Id);

        productAfterCancel.Quantity.ShouldBe(5);
        cashAfterCancel.Balance.ShouldBe(50);
        stockAfterCancel.Count(x => x.Type == StockMovementType.Reversal).ShouldBe(1);
        transactionsAfterCancel.Count(x => x.Type == AccountTransactionType.Reversal).ShouldBe(1);
        transactionsAfterCancel.Single(x => x.Type == AccountTransactionType.Reversal).Amount.ShouldBe(-300);
    }

    [Fact]
    public async Task Should_Create_And_Cancel_Customer_Credit_Pos_Sale_With_Current_Account_Effects()
    {
        var product = await CreateProductAsync("POS-CREDIT-001", "869000000002", 4, 80);
        var customer = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = "POS Credit Customer",
            Type = BusinessPartnerType.Customer,
            CreditLimit = 1000
        });

        var sale = await _posSaleAppService.CreateAsync(new CreatePosSaleDto
        {
            PaymentMethod = PosPaymentMethod.CustomerCredit,
            BusinessPartnerId = customer.Id,
            Items = { new CreatePosSaleItemDto { ProductId = product.Id, Quantity = 1 } }
        });

        sale.Status.ShouldBe(NoxStock.Documents.DocumentStatus.Approved);
        sale.GrandTotal.ShouldBe(96);

        var customerAfterSale = await _businessPartnerRepository.GetAsync(customer.Id);
        var productAfterSale = await _productRepository.GetAsync(product.Id);
        var saleTransactions = await _accountTransactionRepository.GetListAsync(x => x.DocumentId == sale.Id);

        customerAfterSale.Balance.ShouldBe(96);
        productAfterSale.Quantity.ShouldBe(3);
        saleTransactions.ShouldBeEmpty();

        await _posSaleAppService.CancelAsync(sale.Id);

        var customerAfterCancel = await _businessPartnerRepository.GetAsync(customer.Id);
        var productAfterCancel = await _productRepository.GetAsync(product.Id);
        customerAfterCancel.Balance.ShouldBe(0);
        productAfterCancel.Quantity.ShouldBe(4);
    }

    [Fact]
    public async Task Should_Find_All_Brand_Alternatives_By_Shared_Oem_Code()
    {
        const string oemCode = "34116775277";
        var delphi = await CreateProductAsync("DELPHI-F10-DISC", "869000000011", 5, 100, oemCode);
        var fourU = await CreateProductAsync("4U-F10-DISC", "869000000012", 5, 100, "34 116 775 277");
        var bmwGroup = await CreateProductAsync("BMW-GROUP-F10-DISC", "869000000013", 5, 100, "34-116-775-277");

        var catalogResults = await _productAppService.GetListAsync(new ProductGetListInput { Filter = "34 116-775 277", MaxResultCount = 10 });
        var posResults = await _posSaleAppService.GetProductsAsync(new PosProductLookupInputDto { Filter = oemCode });

        catalogResults.Items.Select(product => product.Id).ShouldContain(delphi.Id);
        catalogResults.Items.Select(product => product.Id).ShouldContain(fourU.Id);
        catalogResults.Items.Select(product => product.Id).ShouldContain(bmwGroup.Id);
        posResults.Select(product => product.Id).ShouldContain(delphi.Id);
        posResults.Select(product => product.Id).ShouldContain(fourU.Id);
        posResults.Select(product => product.Id).ShouldContain(bmwGroup.Id);
        posResults.Single(product => product.Id == delphi.Id).OemCodes.ShouldBe(oemCode);
    }

    [Fact]
    public async Task Should_Keep_Manual_Product_Sale_Price_Fixed_In_Try()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var exchangeRateRepository = ServiceProvider.GetRequiredService<IRepository<ExchangeRate, Guid>>();
            var usdRate = await exchangeRateRepository.FirstOrDefaultAsync(x => x.Currency == "USD");
            if (usdRate is null)
            {
                await exchangeRateRepository.InsertAsync(new ExchangeRate(Guid.NewGuid(), "USD", 40m, DateTime.UtcNow));
            }
            else
            {
                usdRate.Rate = 40m;
                usdRate.RateDate = DateTime.UtcNow;
                await exchangeRateRepository.UpdateAsync(usdRate);
            }
        });

        var product = await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = "USD POS Product",
            Sku = "POS-USD-001",
            Barcode = "869000000099",
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = "POS",
            PurchasePrice = 20,
            SalePrice = 30,
            Quantity = 5,
            CriticalStockLevel = 1,
            TrackStock = true,
            PurchaseCurrency = "USD",
        });
        var cashAccount = await _accountAppService.CreateAsync(new CreateUpdateAccountDto
        {
            Name = "TRY POS Cash",
            Type = AccountType.Cash,
            IsActive = true
        });

        var lookup = await _posSaleAppService.GetProductsAsync(new PosProductLookupInputDto
        {
            Filter = product.Barcode,
        });
        lookup.Single().Currency.ShouldBe("TRY");
        lookup.Single().SalePrice.ShouldBe(30m);

        var sale = await _posSaleAppService.CreateAsync(new CreatePosSaleDto
        {
            OperationKey = "pos-try-currency-001",
            PaymentMethod = PosPaymentMethod.Cash,
            AccountId = cashAccount.Id,
            Items = { new CreatePosSaleItemDto { ProductId = product.Id, Quantity = 1 } }
        });

        sale.Currency.ShouldBe("TRY");
        sale.GrandTotal.ShouldBe(36m);
    }

    [Fact]
    public async Task Should_Preserve_Manual_Pos_Unit_Price_Override()
    {
        var product = await CreateProductAsync("POS-MANUAL-001", "869000000199", 3, 125);
        var cashAccount = await _accountAppService.CreateAsync(new CreateUpdateAccountDto
        {
            Name = "Manual POS Cash",
            Type = AccountType.Cash,
            IsActive = true
        });

        var sale = await _posSaleAppService.CreateAsync(new CreatePosSaleDto
        {
            PaymentMethod = PosPaymentMethod.Cash,
            AccountId = cashAccount.Id,
            Items =
            {
                new CreatePosSaleItemDto
                {
                    ProductId = product.Id,
                    Quantity = 1,
                    UnitPrice = 99m,
                    IsUnitPriceOverridden = true
                }
            }
        });

        sale.GrandTotal.ShouldBe(118.8m);
    }

    private async Task<ProductDto> CreateProductAsync(string sku, string barcode, decimal quantity, decimal salePrice, string? oemCodes = null)
    {
        return await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = sku,
            Sku = sku,
            Barcode = barcode,
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = "POS",
            PurchasePrice = salePrice / 2,
            SalePrice = salePrice,
            Quantity = quantity,
            CriticalStockLevel = 1,
            TrackStock = true,
            PurchaseCurrency = "TRY",
            OemCodes = oemCodes
        });
    }
}
