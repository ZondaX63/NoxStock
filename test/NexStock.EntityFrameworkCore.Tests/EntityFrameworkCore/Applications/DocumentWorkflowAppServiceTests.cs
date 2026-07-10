using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NexStock.Accounts;
using NexStock.BusinessPartners;
using NexStock.Documents;
using NexStock.Products;
using NexStock.StockMovements;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace NexStock.EntityFrameworkCore.Applications;

public class DocumentWorkflowAppServiceTests : NexStockEntityFrameworkCoreTestBase
{
    private readonly IAccountAppService _accountAppService;
    private readonly IBusinessPartnerAppService _businessPartnerAppService;
    private readonly IDocumentAppService _documentAppService;
    private readonly IDocumentWorkflowAppService _documentWorkflowAppService;
    private readonly IProductAppService _productAppService;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<AccountTransaction, Guid> _accountTransactionRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<StockMovement, Guid> _stockMovementRepository;

    public DocumentWorkflowAppServiceTests()
    {
        _accountAppService = ServiceProvider.GetRequiredService<IAccountAppService>();
        _businessPartnerAppService = ServiceProvider.GetRequiredService<IBusinessPartnerAppService>();
        _documentAppService = ServiceProvider.GetRequiredService<IDocumentAppService>();
        _documentWorkflowAppService = ServiceProvider.GetRequiredService<IDocumentWorkflowAppService>();
        _productAppService = ServiceProvider.GetRequiredService<IProductAppService>();
        _accountRepository = ServiceProvider.GetRequiredService<IRepository<Account, Guid>>();
        _accountTransactionRepository = ServiceProvider.GetRequiredService<IRepository<AccountTransaction, Guid>>();
        _businessPartnerRepository = ServiceProvider.GetRequiredService<IRepository<BusinessPartner, Guid>>();
        _productRepository = ServiceProvider.GetRequiredService<IRepository<Product, Guid>>();
        _stockMovementRepository = ServiceProvider.GetRequiredService<IRepository<StockMovement, Guid>>();
    }

    [Fact]
    public async Task Should_Approve_And_Cancel_Sales_Invoice_With_Ledger_Effects()
    {
        var product = await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = "Prototype Brake Pad",
            Sku = "PROTO-BP-001",
            Barcode = string.Empty,
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = "A-01",
            PurchasePrice = 60,
            SalePrice = 100,
            Quantity = 10,
            CriticalStockLevel = 2,
            TrackStock = true,
            Currency = "TRY",
            PurchaseCurrency = "TRY",
            SaleCurrency = "TRY"
        });

        var customer = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = "Prototype Customer",
            Type = BusinessPartnerType.Customer,
            Balance = 0,
            CreditLimit = 1000,
            Currency = "TRY"
        });

        var cashAccount = await _accountAppService.CreateAsync(new CreateUpdateAccountDto
        {
            Name = "Prototype Cash",
            Type = AccountType.Cash,
            Currency = "TRY",
            Balance = 0,
            IsActive = true
        });

        var document = await _documentAppService.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = customer.Id,
            DocNumber = "SLS-PROTO-001",
            Type = DocumentType.SaleInvoice,
            Status = DocumentStatus.Draft,
            Currency = "TRY",
            ExchangeRate = 1,
            Date = DateTime.UtcNow,
            Items =
            {
                new CreateUpdateDocumentItemDto
                {
                    ProductId = product.Id,
                    Quantity = 2,
                    UnitPrice = 100,
                    VatRate = 0,
                    DiscountRate = 0
                }
            }
        });

        var approved = await _documentWorkflowAppService.ApproveAsync(document.Id, new ApproveDocumentInputDto
        {
            AccountId = cashAccount.Id,
            PaymentAmount = 50
        });

        approved.Status.ShouldBe(DocumentStatus.PartiallyPaid);
        approved.GrandTotal.ShouldBe(200);
        approved.PaidAmount.ShouldBe(50);

        var productAfterApprove = await _productRepository.GetAsync(product.Id);
        var customerAfterApprove = await _businessPartnerRepository.GetAsync(customer.Id);
        var cashAfterApprove = await _accountRepository.GetAsync(cashAccount.Id);
        var movementsAfterApprove = await _stockMovementRepository.GetListAsync(x => x.DocumentId == document.Id);
        var transactionsAfterApprove = await _accountTransactionRepository.GetListAsync(x => x.DocumentId == document.Id);

        productAfterApprove.Quantity.ShouldBe(8);
        customerAfterApprove.Balance.ShouldBe(150);
        cashAfterApprove.Balance.ShouldBe(50);
        movementsAfterApprove.Single().Type.ShouldBe(StockMovementType.SalesInvoice);
        movementsAfterApprove.Single().Quantity.ShouldBe(-2);
        transactionsAfterApprove.Single().Type.ShouldBe(AccountTransactionType.Collection);
        transactionsAfterApprove.Single().Amount.ShouldBe(50);

        var cancelled = await _documentWorkflowAppService.CancelAsync(document.Id);

        cancelled.Status.ShouldBe(DocumentStatus.Cancelled);

        var productAfterCancel = await _productRepository.GetAsync(product.Id);
        var customerAfterCancel = await _businessPartnerRepository.GetAsync(customer.Id);
        var cashAfterCancel = await _accountRepository.GetAsync(cashAccount.Id);
        var movementsAfterCancel = await _stockMovementRepository.GetListAsync(x => x.DocumentId == document.Id);
        var transactionsAfterCancel = await _accountTransactionRepository.GetListAsync(x => x.DocumentId == document.Id);

        productAfterCancel.Quantity.ShouldBe(10);
        customerAfterCancel.Balance.ShouldBe(0);
        cashAfterCancel.Balance.ShouldBe(0);
        movementsAfterCancel.Count(x => x.Type == StockMovementType.Reversal).ShouldBe(1);
        movementsAfterCancel.Single(x => x.Type == StockMovementType.Reversal).Quantity.ShouldBe(2);
        transactionsAfterCancel.Count(x => x.Type == AccountTransactionType.Reversal).ShouldBe(1);
        transactionsAfterCancel.Single(x => x.Type == AccountTransactionType.Reversal).Amount.ShouldBe(-50);
    }

    [Fact]
    public async Task Should_Approve_And_Cancel_Purchase_Invoice_With_Ledger_Effects()
    {
        var product = await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = "Prototype Engine Oil",
            Sku = "PROTO-OIL-001",
            Barcode = string.Empty,
            Description = string.Empty,
            Unit = "Litre",
            ShelfLocation = "B-02",
            PurchasePrice = 80,
            SalePrice = 120,
            Quantity = 20,
            CriticalStockLevel = 5,
            TrackStock = true,
            Currency = "TRY",
            PurchaseCurrency = "TRY",
            SaleCurrency = "TRY"
        });

        var supplier = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = "Prototype Supplier",
            Type = BusinessPartnerType.Supplier,
            Balance = 0,
            CreditLimit = 5000,
            Currency = "TRY"
        });

        var bankAccount = await _accountAppService.CreateAsync(new CreateUpdateAccountDto
        {
            Name = "Prototype Bank",
            Type = AccountType.Bank,
            Currency = "TRY",
            Balance = 1000,
            IsActive = true
        });

        var document = await _documentAppService.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = supplier.Id,
            DocNumber = "PUR-PROTO-001",
            Type = DocumentType.PurchaseInvoice,
            Status = DocumentStatus.Draft,
            Currency = "TRY",
            ExchangeRate = 1,
            Date = DateTime.UtcNow,
            Items =
            {
                new CreateUpdateDocumentItemDto
                {
                    ProductId = product.Id,
                    Quantity = 5,
                    UnitPrice = 80,
                    VatRate = 0,
                    DiscountRate = 0
                }
            }
        });

        var approved = await _documentWorkflowAppService.ApproveAsync(document.Id, new ApproveDocumentInputDto
        {
            AccountId = bankAccount.Id,
            PaymentAmount = 100
        });

        approved.Status.ShouldBe(DocumentStatus.PartiallyPaid);
        approved.GrandTotal.ShouldBe(400);
        approved.PaidAmount.ShouldBe(100);

        var productAfterApprove = await _productRepository.GetAsync(product.Id);
        var supplierAfterApprove = await _businessPartnerRepository.GetAsync(supplier.Id);
        var bankAfterApprove = await _accountRepository.GetAsync(bankAccount.Id);
        var movementsAfterApprove = await _stockMovementRepository.GetListAsync(x => x.DocumentId == document.Id);
        var transactionsAfterApprove = await _accountTransactionRepository.GetListAsync(x => x.DocumentId == document.Id);

        productAfterApprove.Quantity.ShouldBe(25); // 20 + 5
        supplierAfterApprove.Balance.ShouldBe(-300); // 0 - (400 - 100)
        bankAfterApprove.Balance.ShouldBe(900); // 1000 - 100
        movementsAfterApprove.Single().Type.ShouldBe(StockMovementType.PurchaseInvoice);
        movementsAfterApprove.Single().Quantity.ShouldBe(5);
        transactionsAfterApprove.Single().Type.ShouldBe(AccountTransactionType.Payment);
        transactionsAfterApprove.Single().Amount.ShouldBe(-100);

        var cancelled = await _documentWorkflowAppService.CancelAsync(document.Id);

        cancelled.Status.ShouldBe(DocumentStatus.Cancelled);

        var productAfterCancel = await _productRepository.GetAsync(product.Id);
        var supplierAfterCancel = await _businessPartnerRepository.GetAsync(supplier.Id);
        var bankAfterCancel = await _accountRepository.GetAsync(bankAccount.Id);
        var movementsAfterCancel = await _stockMovementRepository.GetListAsync(x => x.DocumentId == document.Id);
        var transactionsAfterCancel = await _accountTransactionRepository.GetListAsync(x => x.DocumentId == document.Id);

        productAfterCancel.Quantity.ShouldBe(20);
        supplierAfterCancel.Balance.ShouldBe(0);
        bankAfterCancel.Balance.ShouldBe(1000);
        movementsAfterCancel.Count(x => x.Type == StockMovementType.Reversal).ShouldBe(1);
        movementsAfterCancel.Single(x => x.Type == StockMovementType.Reversal).Quantity.ShouldBe(-5);
        transactionsAfterCancel.Count(x => x.Type == AccountTransactionType.Reversal).ShouldBe(1);
        transactionsAfterCancel.Single(x => x.Type == AccountTransactionType.Reversal).Amount.ShouldBe(100);
    }

    [Fact]
    public async Task Should_Approve_And_Cancel_Collection()
    {
        var customer = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = "Collection Customer",
            Type = BusinessPartnerType.Customer,
            Balance = 500, // Owes 500
            CreditLimit = 1000,
            Currency = "TRY"
        });

        var cashAccount = await _accountAppService.CreateAsync(new CreateUpdateAccountDto
        {
            Name = "Collection Cash",
            Type = AccountType.Cash,
            Currency = "TRY",
            Balance = 0,
            IsActive = true
        });

        var document = await _documentAppService.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = customer.Id,
            DocNumber = "COL-001",
            Type = DocumentType.Collection,
            Status = DocumentStatus.Draft,
            Currency = "TRY",
            ExchangeRate = 1,
            Date = DateTime.UtcNow,
            GrandTotal = 200
        });

        var approved = await _documentWorkflowAppService.ApproveAsync(document.Id, new ApproveDocumentInputDto
        {
            AccountId = cashAccount.Id,
            PaymentAmount = 200
        });

        approved.Status.ShouldBe(DocumentStatus.Completed);
        approved.GrandTotal.ShouldBe(200);

        var customerAfterApprove = await _businessPartnerRepository.GetAsync(customer.Id);
        var cashAfterApprove = await _accountRepository.GetAsync(cashAccount.Id);
        var transactionsAfterApprove = await _accountTransactionRepository.GetListAsync(x => x.DocumentId == document.Id);

        customerAfterApprove.Balance.ShouldBe(300); // 500 - 200
        cashAfterApprove.Balance.ShouldBe(200); // 0 + 200
        transactionsAfterApprove.Single().Type.ShouldBe(AccountTransactionType.Collection);
        transactionsAfterApprove.Single().Amount.ShouldBe(200);

        var cancelled = await _documentWorkflowAppService.CancelAsync(document.Id);

        cancelled.Status.ShouldBe(DocumentStatus.Cancelled);

        var customerAfterCancel = await _businessPartnerRepository.GetAsync(customer.Id);
        var cashAfterCancel = await _accountRepository.GetAsync(cashAccount.Id);
        var transactionsAfterCancel = await _accountTransactionRepository.GetListAsync(x => x.DocumentId == document.Id);

        customerAfterCancel.Balance.ShouldBe(500);
        cashAfterCancel.Balance.ShouldBe(0);
        transactionsAfterCancel.Count(x => x.Type == AccountTransactionType.Reversal).ShouldBe(1);
        transactionsAfterCancel.Single(x => x.Type == AccountTransactionType.Reversal).Amount.ShouldBe(-200);
    }
}
