using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.Accounts;
using NoxStock.BusinessPartners;
using NoxStock.Documents;
using NoxStock.Products;
using NoxStock.StockMovements;
using NoxStock.Settings;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace NoxStock.EntityFrameworkCore.Applications;

public class DocumentWorkflowAppServiceTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly IAccountAppService _accountAppService;
    private readonly IBusinessPartnerAppService _businessPartnerAppService;
    private readonly IBusinessPartnerStatementAppService _businessPartnerStatementAppService;
    private readonly IDocumentAppService _documentAppService;
    private readonly IDocumentWorkflowAppService _documentWorkflowAppService;
    private readonly IProductAppService _productAppService;
    private readonly ICompanySettingsAppService _companySettingsAppService;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<AccountTransaction, Guid> _accountTransactionRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<StockMovement, Guid> _stockMovementRepository;

    public DocumentWorkflowAppServiceTests()
    {
        _accountAppService = ServiceProvider.GetRequiredService<IAccountAppService>();
        _businessPartnerAppService = ServiceProvider.GetRequiredService<IBusinessPartnerAppService>();
        _businessPartnerStatementAppService = ServiceProvider.GetRequiredService<IBusinessPartnerStatementAppService>();
        _documentAppService = ServiceProvider.GetRequiredService<IDocumentAppService>();
        _documentWorkflowAppService = ServiceProvider.GetRequiredService<IDocumentWorkflowAppService>();
        _productAppService = ServiceProvider.GetRequiredService<IProductAppService>();
        _companySettingsAppService = ServiceProvider.GetRequiredService<ICompanySettingsAppService>();
        _accountRepository = ServiceProvider.GetRequiredService<IRepository<Account, Guid>>();
        _accountTransactionRepository = ServiceProvider.GetRequiredService<IRepository<AccountTransaction, Guid>>();
        _businessPartnerRepository = ServiceProvider.GetRequiredService<IRepository<BusinessPartner, Guid>>();
        _productRepository = ServiceProvider.GetRequiredService<IRepository<Product, Guid>>();
        _stockMovementRepository = ServiceProvider.GetRequiredService<IRepository<StockMovement, Guid>>();
    }

    [Fact]
    public async Task Should_Approve_And_Cancel_Sales_Invoice_With_Ledger_Effects()
    {
        var companySettings = new CompanySettingsDto
        {
            CompanyName = "Snapshot Company A.S.",
            Address = "Snapshot company address",
            Phone = "5550000000",
            Email = "snapshot@noxstock.test",
            TaxOffice = "Snapshot tax office",
            TaxNumber = "1234567890",
            DefaultCurrency = "TRY",
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
        await _companySettingsAppService.UpdateAsync(companySettings);

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
            PurchaseCurrency = "TRY",
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
        approved.GrandTotal.ShouldBe(240);
        approved.PaidAmount.ShouldBe(50);
        approved.PrintSnapshotCapturedAt.ShouldNotBeNull();
        approved.CompanyNameSnapshot.ShouldBe("Snapshot Company A.S.");
        approved.BusinessPartnerNameSnapshot.ShouldBe("Prototype Customer");
        approved.Items.Single().ProductNameSnapshot.ShouldBe("Prototype Brake Pad");
        approved.Items.Single().ProductSkuSnapshot.ShouldBe("PROTO-BP-001");
        approved.Items.Single().ProductUnitSnapshot.ShouldBe("Adet");

        var duplicateApproval = await Should.ThrowAsync<BusinessException>(() =>
            _documentWorkflowAppService.ApproveAsync(document.Id, new ApproveDocumentInputDto()));
        duplicateApproval.Code.ShouldBe("NoxStock:DocumentStatusNotApprovable");

        var productAfterApprove = await _productRepository.GetAsync(product.Id);
        var customerAfterApprove = await _businessPartnerRepository.GetAsync(customer.Id);
        var cashAfterApprove = await _accountRepository.GetAsync(cashAccount.Id);
        var movementsAfterApprove = await _stockMovementRepository.GetListAsync(x => x.DocumentId == document.Id);
        var transactionsAfterApprove = await _accountTransactionRepository.GetListAsync(x => x.DocumentId == document.Id);

        productAfterApprove.Quantity.ShouldBe(8);
        customerAfterApprove.Balance.ShouldBe(190);
        cashAfterApprove.Balance.ShouldBe(50);
        movementsAfterApprove.Single().Type.ShouldBe(StockMovementType.SalesInvoice);
        movementsAfterApprove.Single().Quantity.ShouldBe(-2);
        transactionsAfterApprove.Single().Type.ShouldBe(AccountTransactionType.Collection);
        transactionsAfterApprove.Single().Amount.ShouldBe(50);

        var changedPartner = await _businessPartnerRepository.GetAsync(customer.Id);
        changedPartner.Name = "Changed customer name";
        changedPartner.Address = "Changed customer address";
        await _businessPartnerRepository.UpdateAsync(changedPartner, autoSave: true);
        var changedProduct = await _productRepository.GetAsync(product.Id);
        changedProduct.Name = "Changed product name";
        changedProduct.Sku = "CHANGED-SKU";
        await _productRepository.UpdateAsync(changedProduct, autoSave: true);
        companySettings.CompanyName = "Changed company name";
        companySettings.Address = "Changed company address";
        await _companySettingsAppService.UpdateAsync(companySettings);

        var finalizedPrintDto = await _documentAppService.GetAsync(document.Id);
        finalizedPrintDto.CompanyNameSnapshot.ShouldBe("Snapshot Company A.S.");
        finalizedPrintDto.BusinessPartnerName.ShouldBe("Prototype Customer");
        finalizedPrintDto.Items.Single().ProductName.ShouldBe("Prototype Brake Pad");
        finalizedPrintDto.Items.Single().ProductSkuSnapshot.ShouldBe("PROTO-BP-001");

        var paymentInput = new RecordDocumentPaymentInputDto
        {
            OperationKey = "sales-payment-001",
            AccountId = cashAccount.Id,
            Amount = 190,
            PaymentDate = DateTime.UtcNow,
            Description = "Kalan tutar tahsil edildi"
        };
        var paid = await _documentWorkflowAppService.RecordPaymentAsync(document.Id, paymentInput);
        var replayedPayment = await _documentWorkflowAppService.RecordPaymentAsync(document.Id, paymentInput);

        paid.Status.ShouldBe(DocumentStatus.Paid);
        paid.PaidAmount.ShouldBe(240);
        replayedPayment.PaidAmount.ShouldBe(240);

        var customerAfterPayment = await _businessPartnerRepository.GetAsync(customer.Id);
        var cashAfterPayment = await _accountRepository.GetAsync(cashAccount.Id);
        var transactionsAfterPayment = await _accountTransactionRepository.GetListAsync(x => x.DocumentId == document.Id);

        customerAfterPayment.Balance.ShouldBe(0);
        cashAfterPayment.Balance.ShouldBe(240);
        transactionsAfterPayment.Count(x => x.Type == AccountTransactionType.Collection).ShouldBe(2);
        transactionsAfterPayment.Sum(x => x.Amount).ShouldBe(240);

        var paymentHistory = await _businessPartnerStatementAppService.GetPaymentHistoryAsync(customer.Id);
        paymentHistory.Count.ShouldBe(2);
        paymentHistory.All(x => x.DocumentNumber == document.DocNumber).ShouldBeTrue();
        paymentHistory.All(x => x.AccountName == cashAccount.Name).ShouldBeTrue();

        var cancelled = await _documentWorkflowAppService.CancelAsync(document.Id);

        cancelled.Status.ShouldBe(DocumentStatus.Cancelled);
        cancelled.CompanyNameSnapshot.ShouldBe("Snapshot Company A.S.");
        cancelled.BusinessPartnerNameSnapshot.ShouldBe("Prototype Customer");
        cancelled.Items.Single().ProductNameSnapshot.ShouldBe("Prototype Brake Pad");

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
        transactionsAfterCancel.Count(x => x.Type == AccountTransactionType.Reversal).ShouldBe(2);
        transactionsAfterCancel.Where(x => x.Type == AccountTransactionType.Reversal).Sum(x => x.Amount).ShouldBe(-240);

        var duplicateCancellation = await Should.ThrowAsync<BusinessException>(() =>
            _documentWorkflowAppService.CancelAsync(document.Id));
        duplicateCancellation.Code.ShouldBe("NoxStock:DocumentStatusNotCancellable");

        (await _stockMovementRepository.GetListAsync(x => x.DocumentId == document.Id))
            .Count(x => x.Type == StockMovementType.Reversal).ShouldBe(1);
        (await _accountTransactionRepository.GetListAsync(x => x.DocumentId == document.Id))
            .Count(x => x.Type == AccountTransactionType.Reversal).ShouldBe(2);
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
            PurchaseCurrency = "TRY",
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
        // CreateAsync creates a zero-opening account; an opening balance belongs
        // to CreateWithOpeningAsync/CreateOpeningBalanceAsync.
        bankAfterApprove.Balance.ShouldBe(-100);
        movementsAfterApprove.Single().Type.ShouldBe(StockMovementType.PurchaseInvoice);
        movementsAfterApprove.Single().Quantity.ShouldBe(5);
        transactionsAfterApprove.Single().Type.ShouldBe(AccountTransactionType.Payment);
        transactionsAfterApprove.Single().Amount.ShouldBe(-100);

        var paid = await _documentWorkflowAppService.RecordPaymentAsync(document.Id, new RecordDocumentPaymentInputDto
        {
            AccountId = bankAccount.Id,
            Amount = 300,
            PaymentDate = DateTime.UtcNow,
            Description = "Kalan tedarikçi borcu ödendi"
        });

        paid.Status.ShouldBe(DocumentStatus.Paid);
        paid.PaidAmount.ShouldBe(400);

        var supplierAfterPayment = await _businessPartnerRepository.GetAsync(supplier.Id);
        var bankAfterPayment = await _accountRepository.GetAsync(bankAccount.Id);
        var transactionsAfterPayment = await _accountTransactionRepository.GetListAsync(x => x.DocumentId == document.Id);

        supplierAfterPayment.Balance.ShouldBe(0);
        bankAfterPayment.Balance.ShouldBe(-400);
        transactionsAfterPayment.Count(x => x.Type == AccountTransactionType.Payment).ShouldBe(2);
        transactionsAfterPayment.Sum(x => x.Amount).ShouldBe(-400);

        var cancelled = await _documentWorkflowAppService.CancelAsync(document.Id);

        cancelled.Status.ShouldBe(DocumentStatus.Cancelled);

        var productAfterCancel = await _productRepository.GetAsync(product.Id);
        var supplierAfterCancel = await _businessPartnerRepository.GetAsync(supplier.Id);
        var bankAfterCancel = await _accountRepository.GetAsync(bankAccount.Id);
        var movementsAfterCancel = await _stockMovementRepository.GetListAsync(x => x.DocumentId == document.Id);
        var transactionsAfterCancel = await _accountTransactionRepository.GetListAsync(x => x.DocumentId == document.Id);

        productAfterCancel.Quantity.ShouldBe(20);
        supplierAfterCancel.Balance.ShouldBe(0);
        bankAfterCancel.Balance.ShouldBe(0);
        movementsAfterCancel.Count(x => x.Type == StockMovementType.Reversal).ShouldBe(1);
        movementsAfterCancel.Single(x => x.Type == StockMovementType.Reversal).Quantity.ShouldBe(-5);
        transactionsAfterCancel.Count(x => x.Type == AccountTransactionType.Reversal).ShouldBe(2);
        transactionsAfterCancel.Where(x => x.Type == AccountTransactionType.Reversal).Sum(x => x.Amount).ShouldBe(400);
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

        // CreateAsync creates a zero-opening partner; opening exposure is a
        // separate workflow and is not populated from this DTO's Balance field.
        customerAfterApprove.Balance.ShouldBe(-200);
        cashAfterApprove.Balance.ShouldBe(200); // 0 + 200
        transactionsAfterApprove.Single().Type.ShouldBe(AccountTransactionType.Collection);
        transactionsAfterApprove.Single().Amount.ShouldBe(200);

        var cancelled = await _documentWorkflowAppService.CancelAsync(document.Id);

        cancelled.Status.ShouldBe(DocumentStatus.Cancelled);

        var customerAfterCancel = await _businessPartnerRepository.GetAsync(customer.Id);
        var cashAfterCancel = await _accountRepository.GetAsync(cashAccount.Id);
        var transactionsAfterCancel = await _accountTransactionRepository.GetListAsync(x => x.DocumentId == document.Id);

        customerAfterCancel.Balance.ShouldBe(0);
        cashAfterCancel.Balance.ShouldBe(0);
        transactionsAfterCancel.Count(x => x.Type == AccountTransactionType.Reversal).ShouldBe(1);
        transactionsAfterCancel.Single(x => x.Type == AccountTransactionType.Reversal).Amount.ShouldBe(-200);
    }

    [Fact]
    public async Task Should_Use_Live_Partner_And_Product_Data_For_Draft_Document_Output()
    {
        var product = await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = "Draft product before change",
            Sku = "DRAFT-SNAPSHOT-001",
            Barcode = string.Empty,
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = "Draft",
            PurchasePrice = 10,
            SalePrice = 20,
            Quantity = 3,
            CriticalStockLevel = 0,
            TrackStock = true,
            PurchaseCurrency = "TRY"
        });
        var partner = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = "Draft partner before change",
            Type = BusinessPartnerType.Customer,
            Currency = "TRY"
        });
        var draft = await CreateSaleInvoiceAsync(
            partner.Id, "DRAFT-SNAPSHOT-001", (product.Id, 1));

        var changedProduct = await _productRepository.GetAsync(product.Id);
        changedProduct.Name = "Draft product after change";
        await _productRepository.UpdateAsync(changedProduct, autoSave: true);
        var changedPartner = await _businessPartnerRepository.GetAsync(partner.Id);
        changedPartner.Name = "Draft partner after change";
        await _businessPartnerRepository.UpdateAsync(changedPartner, autoSave: true);

        var document = await _documentAppService.GetAsync(draft.Id);
        document.PrintSnapshotCapturedAt.ShouldBeNull();
        document.BusinessPartnerName.ShouldBe("Draft partner after change");
        document.Items.Single().ProductName.ShouldBe("Draft product after change");
    }

    [Fact]
    public async Task Should_Not_Approve_Sale_Invoice_When_Stock_Is_Insufficient_And_Leave_No_Postings()
    {
        var product = await CreateProductAsync("STOCK-INSUFFICIENT-001", 2, true);
        var secondProduct = await CreateProductAsync("STOCK-INSUFFICIENT-002", 1, true);
        var customer = await CreateCustomerAsync("Insufficient stock customer");
        var document = await CreateSaleInvoiceAsync(
            customer.Id,
            "STOCK-INSUFFICIENT-INV-001",
            (product.Id, 3),
            (secondProduct.Id, 2));

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _documentWorkflowAppService.ApproveAsync(document.Id, new ApproveDocumentInputDto()));

        exception.Code.ShouldBe("NoxStock:InsufficientStock");
        var insufficientProducts = exception.Data["InsufficientProducts"]?.ToString();
        insufficientProducts.ShouldNotBeNull();
        insufficientProducts!.ShouldContain(product.Id.ToString());
        insufficientProducts.ShouldContain(product.Sku);
        insufficientProducts.ShouldContain(secondProduct.Id.ToString());
        insufficientProducts.ShouldContain(secondProduct.Sku);
        insufficientProducts.ShouldContain("mevcut 2");
        insufficientProducts.ShouldContain("istenen 3");

        var state = await ReadDocumentPostingStateAsync(document.Id, product.Id, customer.Id);
        state.Document.Status.ShouldBe(DocumentStatus.Draft);
        state.Product.Quantity.ShouldBe(2);
        (await _productRepository.GetAsync(secondProduct.Id)).Quantity.ShouldBe(1);
        state.ProductMovements.ShouldBeEmpty();
        state.AccountTransactions.ShouldBeEmpty();
        state.BusinessPartner.Balance.ShouldBe(0);
    }

    [Fact]
    public async Task Should_Approve_Sale_Invoice_When_Stock_Exactly_Matches_Demand()
    {
        var product = await CreateProductAsync("STOCK-EXACT-001", 3, true);
        var customer = await CreateCustomerAsync("Exact stock customer");
        var document = await CreateSaleInvoiceAsync(customer.Id, "STOCK-EXACT-INV-001", (product.Id, 3));

        var approved = await _documentWorkflowAppService.ApproveAsync(document.Id, new ApproveDocumentInputDto());

        approved.Status.ShouldBe(DocumentStatus.Approved);
        (await _productRepository.GetAsync(product.Id)).Quantity.ShouldBe(0);
        (await _stockMovementRepository.GetListAsync(x => x.DocumentId == document.Id)).Single().Quantity.ShouldBe(-3);
    }

    [Fact]
    public async Task Should_Validate_Combined_Demand_For_Duplicate_Product_Lines()
    {
        var product = await CreateProductAsync("STOCK-GROUPED-001", 3, true);
        var customer = await CreateCustomerAsync("Grouped stock customer");
        var document = await CreateSaleInvoiceAsync(
            customer.Id,
            "STOCK-GROUPED-INV-001",
            (product.Id, 2),
            (product.Id, 2));

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _documentWorkflowAppService.ApproveAsync(document.Id, new ApproveDocumentInputDto()));

        exception.Code.ShouldBe("NoxStock:InsufficientStock");
        var insufficientProducts = exception.Data["InsufficientProducts"]?.ToString();
        insufficientProducts.ShouldNotBeNull();
        insufficientProducts!.ShouldContain("istenen 4");
        (await _productRepository.GetAsync(product.Id)).Quantity.ShouldBe(3);
        (await _stockMovementRepository.GetListAsync(x => x.DocumentId == document.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Preserve_NonTracked_Product_Sale_Behavior()
    {
        var product = await CreateProductAsync("NON-TRACKED-001", 0, false);
        var customer = await CreateCustomerAsync("Non-tracked product customer");
        var document = await CreateSaleInvoiceAsync(customer.Id, "NON-TRACKED-INV-001", (product.Id, 10));

        var approved = await _documentWorkflowAppService.ApproveAsync(document.Id, new ApproveDocumentInputDto());

        approved.Status.ShouldBe(DocumentStatus.Approved);
        (await _productRepository.GetAsync(product.Id)).Quantity.ShouldBe(0);
        (await _stockMovementRepository.GetListAsync(x => x.DocumentId == document.Id)).ShouldBeEmpty();
    }

    private async Task<ProductDto> CreateProductAsync(string sku, decimal quantity, bool trackStock)
    {
        return await _productAppService.CreateAsync(new CreateUpdateProductDto
        {
            Name = sku,
            Sku = sku,
            Barcode = string.Empty,
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = "Test",
            PurchasePrice = 10,
            SalePrice = 20,
            Quantity = quantity,
            CriticalStockLevel = 0,
            TrackStock = trackStock,
            PurchaseCurrency = "TRY"
        });
    }

    private async Task<BusinessPartnerDto> CreateCustomerAsync(string name)
    {
        return await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = name,
            Type = BusinessPartnerType.Customer,
            Currency = "TRY",
            CreditLimit = 100000
        });
    }

    private async Task<DocumentDto> CreateSaleInvoiceAsync(
        Guid customerId,
        string documentNumber,
        params (Guid ProductId, decimal Quantity)[] lines)
    {
        var input = new CreateUpdateDocumentDto
        {
            BusinessPartnerId = customerId,
            DocNumber = documentNumber,
            Type = DocumentType.SaleInvoice,
            Status = DocumentStatus.Draft,
            Currency = "TRY",
            ExchangeRate = 1,
            Date = DateTime.UtcNow
        };

        foreach (var line in lines)
        {
            input.Items.Add(new CreateUpdateDocumentItemDto
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitPrice = 20,
                VatRate = 0,
                DiscountRate = 0
            });
        }

        return await _documentAppService.CreateAsync(input);
    }

    private async Task<DocumentPostingState> ReadDocumentPostingStateAsync(Guid documentId, Guid productId, Guid customerId)
    {
        using var scope = ServiceProvider.CreateScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ICurrentTenant>()
            .Change(NoxStockTestConsts.TenantId, "NoxStock Test Tenant");
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var uow = uowManager.Begin();

        var document = await scope.ServiceProvider.GetRequiredService<IRepository<Document, Guid>>().GetAsync(documentId);
        var product = await scope.ServiceProvider.GetRequiredService<IRepository<Product, Guid>>().GetAsync(productId);
        var partner = await scope.ServiceProvider.GetRequiredService<IRepository<BusinessPartner, Guid>>().GetAsync(customerId);
        var movements = await scope.ServiceProvider.GetRequiredService<IRepository<StockMovement, Guid>>()
            .GetListAsync(movement => movement.DocumentId == documentId);
        var transactions = await scope.ServiceProvider.GetRequiredService<IRepository<AccountTransaction, Guid>>()
            .GetListAsync(transaction => transaction.DocumentId == documentId);

        await uow.CompleteAsync();
        return new DocumentPostingState(document, product, partner, movements, transactions);
    }

    private sealed record DocumentPostingState(
        Document Document,
        Product Product,
        BusinessPartner BusinessPartner,
        System.Collections.Generic.List<StockMovement> ProductMovements,
        System.Collections.Generic.List<AccountTransaction> AccountTransactions);
}
