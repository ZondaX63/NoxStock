using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.Accounts;
using NoxStock.BusinessPartners;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace NoxStock.EntityFrameworkCore.Applications;

public class BusinessPartnerPaymentAppServiceTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly IBusinessPartnerAppService _businessPartnerAppService;
    private readonly IAccountAppService _accountAppService;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<AccountTransaction, Guid> _transactionRepository;

    public BusinessPartnerPaymentAppServiceTests()
    {
        _businessPartnerAppService = ServiceProvider.GetRequiredService<IBusinessPartnerAppService>();
        _accountAppService = ServiceProvider.GetRequiredService<IAccountAppService>();
        _businessPartnerRepository = ServiceProvider.GetRequiredService<IRepository<BusinessPartner, Guid>>();
        _accountRepository = ServiceProvider.GetRequiredService<IRepository<Account, Guid>>();
        _transactionRepository = ServiceProvider.GetRequiredService<IRepository<AccountTransaction, Guid>>();
    }

    [Fact]
    public async Task Should_Reject_Collection_When_Account_Currency_Differs_From_Customer()
    {
        var customer = await CreatePartnerAsync("EUR müşteri", BusinessPartnerType.Customer, "EUR", 250m);
        var tryAccount = await CreateAccountAsync("TRY kasa", "TRY", 500m);

        var exception = await Should.ThrowAsync<BusinessException>(() => _businessPartnerAppService.ProcessPaymentAsync(new ProcessPaymentDto
        {
            BusinessPartnerId = customer.Id,
            AccountId = tryAccount.Id,
            IsCollection = true,
            Amount = 50m,
            Date = DateTime.UtcNow
        }));

        exception.Code.ShouldBe("NoxStock:BusinessPartnerPaymentCurrencyMismatch");
        (await _businessPartnerRepository.GetAsync(customer.Id)).Balance.ShouldBe(250m);
        (await _accountRepository.GetAsync(tryAccount.Id)).Balance.ShouldBe(500m);
        (await _transactionRepository.GetListAsync(transaction => transaction.AccountId == tryAccount.Id))
            .Count.ShouldBe(1); // Only the account opening movement exists.
    }

    [Fact]
    public async Task Should_Reject_Collection_For_A_Supplier()
    {
        var supplier = await CreatePartnerAsync("TRY tedarikçi", BusinessPartnerType.Supplier, "TRY", -250m);
        var tryAccount = await CreateAccountAsync("TRY banka", "TRY", 500m);

        var exception = await Should.ThrowAsync<BusinessException>(() => _businessPartnerAppService.ProcessPaymentAsync(new ProcessPaymentDto
        {
            BusinessPartnerId = supplier.Id,
            AccountId = tryAccount.Id,
            IsCollection = true,
            Amount = 50m,
            Date = DateTime.UtcNow
        }));

        exception.Code.ShouldBe("NoxStock:BusinessPartnerPaymentTypeMismatch");
        (await _transactionRepository.GetListAsync(transaction => transaction.AccountId == tryAccount.Id))
            .Count.ShouldBe(1); // Only the account opening movement exists.
    }

    [Fact]
    public async Task Should_Reject_Collection_That_Exceeds_Customer_Receivable()
    {
        var customer = await CreatePartnerAsync("TRY müşteri", BusinessPartnerType.Customer, "TRY", 50m);
        var tryAccount = await CreateAccountAsync("TRY kasa", "TRY", 500m);

        var exception = await Should.ThrowAsync<BusinessException>(() => _businessPartnerAppService.ProcessPaymentAsync(new ProcessPaymentDto
        {
            BusinessPartnerId = customer.Id,
            AccountId = tryAccount.Id,
            IsCollection = true,
            Amount = 51m,
            Date = DateTime.UtcNow
        }));

        exception.Code.ShouldBe("NoxStock:BusinessPartnerCollectionExceedsReceivable");
        (await _businessPartnerRepository.GetAsync(customer.Id)).Balance.ShouldBe(50m);
        (await _accountRepository.GetAsync(tryAccount.Id)).Balance.ShouldBe(500m);
        (await _transactionRepository.GetListAsync(transaction => transaction.AccountId == tryAccount.Id))
            .Count.ShouldBe(1); // Only the account opening movement exists.
    }

    [Fact]
    public async Task Should_Post_Idempotent_Collection_And_Cancel_With_Reversal()
    {
        var customer = await CreatePartnerAsync("Tahsilat müşterisi", BusinessPartnerType.Customer, "TRY", 250m);
        var account = await CreateAccountAsync("Tahsilat kasası", "TRY", 100m);
        var input = new ProcessPaymentDto
        {
            BusinessPartnerId = customer.Id,
            AccountId = account.Id,
            IsCollection = true,
            Amount = 75m,
            OperationKey = "collection-retry-test"
        };

        await _businessPartnerAppService.ProcessPaymentAsync(input);
        await _businessPartnerAppService.ProcessPaymentAsync(input);
        (await _businessPartnerRepository.GetAsync(customer.Id)).Balance.ShouldBe(175m);
        (await _accountRepository.GetAsync(account.Id)).Balance.ShouldBe(175m);
        var transaction = (await _transactionRepository.GetListAsync(x => x.OperationKey == input.OperationKey)).Single();

        await _businessPartnerAppService.CancelPaymentAsync(transaction.Id,
            new CancelBusinessPartnerPaymentDto { Reason = "Yanlış tahsilat" });
        (await _businessPartnerRepository.GetAsync(customer.Id)).Balance.ShouldBe(250m);
        (await _accountRepository.GetAsync(account.Id)).Balance.ShouldBe(100m);
        (await _transactionRepository.GetAsync(transaction.Id)).Status.ShouldBe(AccountTransactionStatus.Cancelled);
        (await _transactionRepository.GetListAsync(x => x.ReversalOfTransactionId == transaction.Id)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Post_Supplier_Payment_With_Correct_Sign_And_Require_Cash()
    {
        var supplier = await CreatePartnerAsync("Ödeme tedarikçisi", BusinessPartnerType.Supplier, "TRY", -200m);
        var account = await CreateAccountAsync("Ödeme kasası", "TRY", 100m);
        var insufficient = await Should.ThrowAsync<BusinessException>(() => _businessPartnerAppService.ProcessPaymentAsync(new ProcessPaymentDto
        {
            BusinessPartnerId = supplier.Id, AccountId = account.Id, IsCollection = false, Amount = 125m
        }));
        insufficient.Code.ShouldBe("NoxStock:BusinessPartnerPaymentInsufficientAccountBalance");

        await _businessPartnerAppService.ProcessPaymentAsync(new ProcessPaymentDto
        {
            BusinessPartnerId = supplier.Id, AccountId = account.Id, IsCollection = false, Amount = 80m
        });
        (await _businessPartnerRepository.GetAsync(supplier.Id)).Balance.ShouldBe(-120m);
        (await _accountRepository.GetAsync(account.Id)).Balance.ShouldBe(20m);
    }

    private async Task<BusinessPartnerDto> CreatePartnerAsync(string name, BusinessPartnerType type, string currency, decimal balance)
    {
        var partner = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = name,
            Type = type,
            Currency = currency,
            Balance = balance
        });
        if (balance != 0m)
        {
            await _businessPartnerAppService.CreateOpeningBalanceAsync(new CreateBusinessPartnerOpeningBalanceDto
            {
                BusinessPartnerId = partner.Id,
                Direction = balance > 0m ? BusinessPartnerOpeningDirection.ReceivableFromPartner : BusinessPartnerOpeningDirection.PayableToPartner,
                Amount = Math.Abs(balance)
            });
        }
        return partner;
    }

    private Task<AccountDto> CreateAccountAsync(string name, string currency, decimal balance)
    {
        return _accountAppService.CreateWithOpeningAsync(new CreateAccountWithOpeningDto
        {
            Name = name,
            Type = AccountType.Cash,
            Currency = currency,
            OpeningAmount = balance,
            IsActive = true
        });
    }
}
