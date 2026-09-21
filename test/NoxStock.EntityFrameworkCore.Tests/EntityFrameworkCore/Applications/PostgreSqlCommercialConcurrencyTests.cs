using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.Accounts;
using NoxStock.BusinessPartners;
using NoxStock.Documents;
using NoxStock.Products;
using NoxStock.Sales;
using NoxStock.StockMovements;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace NoxStock.EntityFrameworkCore.Applications;

public class PostgreSqlCommercialConcurrencyTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly IProductAppService _products;
    private readonly IBusinessPartnerAppService _partners;
    private readonly IDocumentAppService _documents;
    private readonly IAccountAppService _accounts;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<StockMovement, Guid> _movementRepository;
    private readonly IRepository<AccountTransaction, Guid> _transactionRepository;
    private readonly IRepository<CommercialCommand, Guid> _commandRepository;

    public PostgreSqlCommercialConcurrencyTests()
    {
        _products = ServiceProvider.GetRequiredService<IProductAppService>();
        _partners = ServiceProvider.GetRequiredService<IBusinessPartnerAppService>();
        _documents = ServiceProvider.GetRequiredService<IDocumentAppService>();
        _accounts = ServiceProvider.GetRequiredService<IAccountAppService>();
        _productRepository = ServiceProvider.GetRequiredService<IRepository<Product, Guid>>();
        _documentRepository = ServiceProvider.GetRequiredService<IRepository<Document, Guid>>();
        _movementRepository = ServiceProvider.GetRequiredService<IRepository<StockMovement, Guid>>();
        _transactionRepository = ServiceProvider.GetRequiredService<IRepository<AccountTransaction, Guid>>();
        _commandRepository = ServiceProvider.GetRequiredService<IRepository<CommercialCommand, Guid>>();
    }

    [PostgreSqlFact]
    public async Task Same_Invoice_Approved_Concurrently_Leaves_One_Posting_Set()
    {
        var product = await CreateProductAsync("PG-SAME-INVOICE", 1);
        var customer = await CreateCustomerAsync("PG same-invoice customer");
        var invoice = await CreateInvoiceAsync(customer.Id, "PG-SAME-INVOICE-001", product.Id);

        var attempts = await Task.WhenAll(
            ApproveInNewScopeAsync(invoice.Id, "pg-same-invoice-key"),
            ApproveInNewScopeAsync(invoice.Id, "pg-same-invoice-key"));

        attempts.All(attempt => attempt.Succeeded || attempt.ErrorCode == "NoxStock:CommandInProgress").ShouldBeTrue();
        (await _productRepository.GetAsync(product.Id)).Quantity.ShouldBe(0);
        (await _movementRepository.GetListAsync(x => x.DocumentId == invoice.Id)).Count.ShouldBe(1);
        (await _transactionRepository.GetListAsync(x => x.DocumentId == invoice.Id)).ShouldBeEmpty();
    }

    [PostgreSqlFact]
    public async Task Competing_Sale_Invoices_Cannot_Create_Negative_Stock_Or_Partial_Postings()
    {
        var product = await CreateProductAsync("PG-COMPETING-INVOICES", 1);
        var customer = await CreateCustomerAsync("PG competing-invoice customer");
        var first = await CreateInvoiceAsync(customer.Id, "PG-COMPETING-001", product.Id);
        var second = await CreateInvoiceAsync(customer.Id, "PG-COMPETING-002", product.Id);

        var attempts = await Task.WhenAll(ApproveInNewScopeAsync(first.Id), ApproveInNewScopeAsync(second.Id));

        attempts.Count(attempt => attempt.Succeeded).ShouldBeLessThanOrEqualTo(1);
        attempts.Count(attempt => attempt.Succeeded).ShouldBe(1);
        attempts.Single(attempt => !attempt.Succeeded).ErrorCode.ShouldBeOneOf(
            "NoxStock:InsufficientStock",
            "NoxStock:StockChanged");

        (await _productRepository.GetAsync(product.Id)).Quantity.ShouldBe(0);
        (await _movementRepository.GetListAsync(x => x.ProductId == product.Id)).Count.ShouldBe(1);
        (await _transactionRepository.GetListAsync()).ShouldBeEmpty();

        var documents = await _documentRepository.GetListAsync(x => x.Id == first.Id || x.Id == second.Id);
        documents.Count(document => document.Status == DocumentStatus.Approved).ShouldBe(1);
        documents.Count(document => document.Status == DocumentStatus.Draft).ShouldBe(1);
    }

    [PostgreSqlFact]
    public async Task Competing_Pos_Sales_Cannot_Create_Negative_Stock_Or_Partial_Account_Postings()
    {
        var product = await CreateProductAsync("PG-COMPETING-POS", 1);
        var cash = await _accounts.CreateAsync(new CreateUpdateAccountDto
        {
            Name = "PG POS cash",
            Type = AccountType.Cash,
            Currency = "TRY",
            IsActive = true
        });

        var attempts = await Task.WhenAll(
            CreatePosSaleInNewScopeAsync(product.Id, cash.Id, "pg-pos-a"),
            CreatePosSaleInNewScopeAsync(product.Id, cash.Id, "pg-pos-b"));

        attempts.Count(attempt => attempt.Succeeded).ShouldBeLessThanOrEqualTo(1);
        (await _productRepository.GetAsync(product.Id)).Quantity.ShouldBeGreaterThanOrEqualTo(0);
        (await _movementRepository.GetListAsync(x => x.ProductId == product.Id)).Count.ShouldBe(attempts.Count(x => x.Succeeded));
        (await _transactionRepository.GetListAsync()).Count.ShouldBe(attempts.Count(x => x.Succeeded));
    }

    [PostgreSqlFact]
    public async Task Same_Pos_Operation_Key_Concurrently_Creates_One_Document_And_One_Posting_Set()
    {
        var product = await CreateProductAsync("PG-SAME-POS", 1);
        var cash = await _accounts.CreateAsync(new CreateUpdateAccountDto
        {
            Name = "PG same POS cash", Type = AccountType.Cash, Currency = "TRY", IsActive = true
        });

        var attempts = await Task.WhenAll(
            CreatePosSaleInNewScopeAsync(product.Id, cash.Id, "pg-same-pos-key"),
            CreatePosSaleInNewScopeAsync(product.Id, cash.Id, "pg-same-pos-key"));

        attempts.All(attempt => attempt.Succeeded || attempt.ErrorCode == "NoxStock:CommandInProgress").ShouldBeTrue();
        (await _documentRepository.GetListAsync(x => x.Type == DocumentType.PosSale)).Count.ShouldBe(1);
        (await _movementRepository.GetListAsync(x => x.ProductId == product.Id)).Count.ShouldBe(1);
        (await _transactionRepository.GetListAsync()).Count.ShouldBe(1);
    }

    [PostgreSqlFact]
    public async Task Same_Cancel_Operation_Key_Concurrently_Creates_One_Reversal_Set()
    {
        var product = await CreateProductAsync("PG-SAME-CANCEL", 1);
        var customer = await CreateCustomerAsync("PG same-cancel customer");
        var invoice = await CreateInvoiceAsync(customer.Id, "PG-SAME-CANCEL-001", product.Id);
        await ApproveInNewScopeAsync(invoice.Id, "pg-approve-before-cancel");

        var attempts = await Task.WhenAll(
            CancelInvoiceInNewScopeAsync(invoice.Id, "pg-same-cancel-key"),
            CancelInvoiceInNewScopeAsync(invoice.Id, "pg-same-cancel-key"));

        attempts.All(attempt => attempt.Succeeded || attempt.ErrorCode == "NoxStock:CommandInProgress").ShouldBeTrue();
        (await _movementRepository.GetListAsync(x => x.DocumentId == invoice.Id)).Count.ShouldBe(2);
        (await _documentRepository.GetAsync(invoice.Id)).Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [PostgreSqlFact]
    public async Task Insufficient_Stock_Fails_Command_Without_Partial_Posting()
    {
        var product = await CreateProductAsync("PG-FAILED-COMMAND", 1);
        var customer = await CreateCustomerAsync("PG failed-command customer");
        var invoice = await CreateInvoiceAsync(customer.Id, "PG-FAILED-COMMAND-001", product.Id, 2);

        var attempt = await ApproveInNewScopeAsync(invoice.Id, "pg-failed-command-key");

        attempt.Succeeded.ShouldBeFalse();
        attempt.ErrorCode.ShouldBe("NoxStock:InsufficientStock");
        (await _productRepository.GetAsync(product.Id)).Quantity.ShouldBe(1);
        (await _movementRepository.GetListAsync(x => x.DocumentId == invoice.Id)).ShouldBeEmpty();
        (await _transactionRepository.GetListAsync(x => x.DocumentId == invoice.Id)).ShouldBeEmpty();
        (await _documentRepository.GetAsync(invoice.Id)).Status.ShouldBe(DocumentStatus.Draft);
        (await GetCommandInNewScopeAsync("pg-failed-command-key"))!
            .Status.ShouldBe(CommercialCommandStatus.Failed);
    }

    private async Task<ProductDto> CreateProductAsync(string sku, decimal quantity)
    {
        return await _products.CreateAsync(new CreateUpdateProductDto
        {
            Name = sku,
            Sku = sku,
            Barcode = string.Empty,
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = "PostgreSQL",
            PurchasePrice = 10,
            SalePrice = 20,
            Quantity = quantity,
            TrackStock = true,
            PurchaseCurrency = "TRY"
        });
    }

    private async Task<BusinessPartnerDto> CreateCustomerAsync(string name) =>
        await _partners.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = name,
            Type = BusinessPartnerType.Customer,
            Currency = "TRY",
            CreditLimit = 100000
        });

    private async Task<DocumentDto> CreateInvoiceAsync(Guid customerId, string number, Guid productId, decimal quantity = 1) =>
        await _documents.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = customerId,
            DocNumber = number,
            Type = DocumentType.SaleInvoice,
            Status = DocumentStatus.Draft,
            Currency = "TRY",
            ExchangeRate = 1,
            Date = DateTime.UtcNow,
            Items =
            {
                new CreateUpdateDocumentItemDto
                {
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPrice = 20,
                    VatRate = 0,
                    DiscountRate = 0
                }
            }
        });

    private async Task<CommercialAttempt> ApproveInNewScopeAsync(Guid documentId, string? operationKey = null)
    {
        using var scope = ServiceProvider.CreateScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ICurrentTenant>()
            .Change(NoxStockTestConsts.TenantId, "NoxStock Test Tenant");
        var workflow = scope.ServiceProvider.GetRequiredService<IDocumentWorkflowAppService>();

        try
        {
            await workflow.ApproveAsync(documentId, new ApproveDocumentInputDto { OperationKey = operationKey });
            return CommercialAttempt.Success;
        }
        catch (BusinessException exception)
        {
            return new CommercialAttempt(false, exception.Code);
        }
    }

    private async Task<CommercialAttempt> CreatePosSaleInNewScopeAsync(Guid productId, Guid accountId, string operationKey)
    {
        using var scope = ServiceProvider.CreateScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ICurrentTenant>()
            .Change(NoxStockTestConsts.TenantId, "NoxStock Test Tenant");
        var pos = scope.ServiceProvider.GetRequiredService<IPosSaleAppService>();

        try
        {
            await pos.CreateAsync(new CreatePosSaleDto
            {
                OperationKey = operationKey,
                PaymentMethod = PosPaymentMethod.Cash,
                AccountId = accountId,
                Items = { new CreatePosSaleItemDto { ProductId = productId, Quantity = 1 } }
            });
            return CommercialAttempt.Success;
        }
        catch (BusinessException exception)
        {
            return new CommercialAttempt(false, exception.Code);
        }
        catch (Exception)
        {
            return new CommercialAttempt(false, null);
        }
    }

    private async Task<CommercialAttempt> CancelInvoiceInNewScopeAsync(Guid documentId, string operationKey)
    {
        using var scope = ServiceProvider.CreateScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ICurrentTenant>()
            .Change(NoxStockTestConsts.TenantId, "NoxStock Test Tenant");
        var workflow = scope.ServiceProvider.GetRequiredService<IDocumentWorkflowAppService>();
        try
        {
            await workflow.CancelAsync(documentId, new CancelDocumentInputDto { OperationKey = operationKey });
            return CommercialAttempt.Success;
        }
        catch (BusinessException exception)
        {
            return new CommercialAttempt(false, exception.Code);
        }
    }

    private async Task<CommercialCommand?> GetCommandInNewScopeAsync(string operationKey)
    {
        using var scope = ServiceProvider.CreateScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ICurrentTenant>()
            .Change(NoxStockTestConsts.TenantId, "NoxStock Test Tenant");
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>()
            .Begin(requiresNew: true, isTransactional: false);
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<CommercialCommand, Guid>>();
        var command = await repository.FirstOrDefaultAsync(x => x.OperationKey == operationKey);
        await uow.CompleteAsync();
        return command;
    }

    private sealed record CommercialAttempt(bool Succeeded, string? ErrorCode)
    {
        public static CommercialAttempt Success { get; } = new(true, null);
    }
}
