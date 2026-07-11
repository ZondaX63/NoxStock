using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NexStock.Accounts;
using NexStock.BusinessPartners;
using NexStock.Products;
using NexStock.Sales;
using NexStock.StockMovements;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace NexStock.EntityFrameworkCore.Applications;

public class PosSaleAppServiceTests : NexStockEntityFrameworkCoreTestBase
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
            Currency = "TRY",
            Balance = 50,
            IsActive = true
        });

        var lookup = await _posSaleAppService.GetProductsAsync(new PosProductLookupInputDto { Filter = product.Barcode });
        lookup.Single().Id.ShouldBe(product.Id);

        var sale = await _posSaleAppService.CreateAsync(new CreatePosSaleDto
        {
            PaymentMethod = PosPaymentMethod.Cash,
            AccountId = cashAccount.Id,
            Items = { new CreatePosSaleItemDto { ProductId = product.Id, Quantity = 2 } }
        });

        sale.Status.ShouldBe(NexStock.Documents.DocumentStatus.Paid);
        sale.GrandTotal.ShouldBe(250);
        sale.PaymentMethod.ShouldBe(PosPaymentMethod.Cash);

        var productAfterSale = await _productRepository.GetAsync(product.Id);
        var cashAfterSale = await _accountRepository.GetAsync(cashAccount.Id);
        var stockMovements = await _stockMovementRepository.GetListAsync(x => x.DocumentId == sale.Id);
        var accountTransactions = await _accountTransactionRepository.GetListAsync(x => x.DocumentId == sale.Id);
        var recentSales = await _posSaleAppService.GetRecentAsync();

        productAfterSale.Quantity.ShouldBe(3);
        cashAfterSale.Balance.ShouldBe(300);
        stockMovements.Single().Type.ShouldBe(StockMovementType.PosSale);
        stockMovements.Single().Quantity.ShouldBe(-2);
        accountTransactions.Single().Type.ShouldBe(AccountTransactionType.Collection);
        accountTransactions.Single().Amount.ShouldBe(250);
        recentSales.Single(x => x.Id == sale.Id).PaymentMethod.ShouldBe(PosPaymentMethod.Cash);

        var cancelled = await _posSaleAppService.CancelAsync(sale.Id);
        cancelled.Status.ShouldBe(NexStock.Documents.DocumentStatus.Cancelled);

        var productAfterCancel = await _productRepository.GetAsync(product.Id);
        var cashAfterCancel = await _accountRepository.GetAsync(cashAccount.Id);
        var stockAfterCancel = await _stockMovementRepository.GetListAsync(x => x.DocumentId == sale.Id);
        var transactionsAfterCancel = await _accountTransactionRepository.GetListAsync(x => x.DocumentId == sale.Id);

        productAfterCancel.Quantity.ShouldBe(5);
        cashAfterCancel.Balance.ShouldBe(50);
        stockAfterCancel.Count(x => x.Type == StockMovementType.Reversal).ShouldBe(1);
        transactionsAfterCancel.Count(x => x.Type == AccountTransactionType.Reversal).ShouldBe(1);
        transactionsAfterCancel.Single(x => x.Type == AccountTransactionType.Reversal).Amount.ShouldBe(-250);
    }

    [Fact]
    public async Task Should_Create_And_Cancel_Customer_Credit_Pos_Sale_With_Current_Account_Effects()
    {
        var product = await CreateProductAsync("POS-CREDIT-001", "869000000002", 4, 80);
        var customer = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = "POS Credit Customer",
            Type = BusinessPartnerType.Customer,
            Currency = "TRY",
            CreditLimit = 1000
        });

        var sale = await _posSaleAppService.CreateAsync(new CreatePosSaleDto
        {
            PaymentMethod = PosPaymentMethod.CustomerCredit,
            BusinessPartnerId = customer.Id,
            Items = { new CreatePosSaleItemDto { ProductId = product.Id, Quantity = 1 } }
        });

        sale.Status.ShouldBe(NexStock.Documents.DocumentStatus.Approved);
        sale.GrandTotal.ShouldBe(80);

        var customerAfterSale = await _businessPartnerRepository.GetAsync(customer.Id);
        var productAfterSale = await _productRepository.GetAsync(product.Id);
        var saleTransactions = await _accountTransactionRepository.GetListAsync(x => x.DocumentId == sale.Id);

        customerAfterSale.Balance.ShouldBe(80);
        productAfterSale.Quantity.ShouldBe(3);
        saleTransactions.ShouldBeEmpty();

        await _posSaleAppService.CancelAsync(sale.Id);

        var customerAfterCancel = await _businessPartnerRepository.GetAsync(customer.Id);
        var productAfterCancel = await _productRepository.GetAsync(product.Id);
        customerAfterCancel.Balance.ShouldBe(0);
        productAfterCancel.Quantity.ShouldBe(4);
    }

    private async Task<ProductDto> CreateProductAsync(string sku, string barcode, decimal quantity, decimal salePrice)
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
            Currency = "TRY",
            PurchaseCurrency = "TRY",
            SaleCurrency = "TRY"
        });
    }
}
