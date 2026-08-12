using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.Accounts;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace NoxStock.EntityFrameworkCore.Applications;

public class AccountOpeningBalanceAppServiceTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly IAccountAppService _accountAppService;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<AccountTransaction, Guid> _transactionRepository;

    public AccountOpeningBalanceAppServiceTests()
    {
        _accountAppService = ServiceProvider.GetRequiredService<IAccountAppService>();
        _accountRepository = ServiceProvider.GetRequiredService<IRepository<Account, Guid>>();
        _transactionRepository = ServiceProvider.GetRequiredService<IRepository<AccountTransaction, Guid>>();
    }

    [Fact]
    public async Task Should_Ignore_Legacy_Balance_During_Normal_Create()
    {
        var account = await _accountAppService.CreateAsync(new CreateUpdateAccountDto
        {
            Name = "Eski istemci hesabı",
            Currency = "TRY",
            Balance = 725m,
            IsActive = true
        });

        (await _accountRepository.GetAsync(account.Id)).Balance.ShouldBe(0m);
        (await _transactionRepository.GetListAsync(transaction => transaction.AccountId == account.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Create_Account_And_Opening_Transaction_Atomically()
    {
        var account = await _accountAppService.CreateWithOpeningAsync(new CreateAccountWithOpeningDto
        {
            Name = "Açılış kasası",
            Type = AccountType.Cash,
            Currency = "EUR",
            OpeningAmount = 150m,
            OpeningDate = new DateTime(2026, 8, 6),
            OpeningDescription = "İlk kasa bakiyesi"
        });

        var persistedAccount = await _accountRepository.GetAsync(account.Id);
        persistedAccount.Balance.ShouldBe(150m);

        var transaction = (await _transactionRepository.GetListAsync(item => item.AccountId == account.Id)).Single();
        transaction.Type.ShouldBe(AccountTransactionType.Opening);
        transaction.Amount.ShouldBe(150m);
        transaction.Currency.ShouldBe("EUR");
        transaction.Description.ShouldBe("İlk kasa bakiyesi");
    }

    [Fact]
    public async Task Should_Reject_Opening_Balance_For_Inactive_Account()
    {
        var inactiveException = await Should.ThrowAsync<BusinessException>(() =>
            _accountAppService.CreateWithOpeningAsync(new CreateAccountWithOpeningDto
            {
                Name = "Pasif açılış hesabı",
                Currency = "TRY",
                IsActive = false,
                OpeningAmount = 10m
            }));

        inactiveException.Code.ShouldBe("NoxStock:AccountOpeningInactiveAccount");
    }

    [Fact]
    public async Task Should_Reject_Zero_Opening_Balance()
    {

        var account = await _accountAppService.CreateAsync(new CreateUpdateAccountDto
        {
            Name = "Mevcut hesap",
            Currency = "TRY",
            IsActive = true
        });

        var zeroException = await Should.ThrowAsync<BusinessException>(() =>
            _accountAppService.CreateOpeningBalanceAsync(new CreateAccountOpeningBalanceDto
            {
                AccountId = account.Id,
                Amount = 0m
            }));
        zeroException.Code.ShouldBe("NoxStock:AccountOpeningAmountMustBePositive");
    }

    [Fact]
    public async Task Should_Reject_Duplicate_Opening_Balance()
    {
        var account = await _accountAppService.CreateAsync(new CreateUpdateAccountDto
        {
            Name = "Tek açılış hesabı",
            Currency = "TRY",
            IsActive = true
        });

        await _accountAppService.CreateOpeningBalanceAsync(new CreateAccountOpeningBalanceDto
        {
            AccountId = account.Id,
            Amount = 25m
        });

        (await _accountRepository.GetAsync(account.Id)).Balance.ShouldBe(25m);
        (await _transactionRepository.GetListAsync(transaction => transaction.AccountId == account.Id)).Count.ShouldBe(1);

        var duplicateException = await Should.ThrowAsync<BusinessException>(() =>
            _accountAppService.CreateOpeningBalanceAsync(new CreateAccountOpeningBalanceDto
            {
                AccountId = account.Id,
                Amount = 5m
            }));

        duplicateException.Code.ShouldBe("NoxStock:AccountOpeningAlreadyExists");
    }

    [Fact]
    public async Task Should_Cancel_Opening_With_Reversal_And_Allow_A_New_Opening()
    {
        var account = await _accountAppService.CreateWithOpeningAsync(new CreateAccountWithOpeningDto
        {
            Name = "İptal edilebilir açılış",
            Currency = "TRY",
            OpeningAmount = 80m
        });
        var opening = (await _transactionRepository.GetListAsync(transaction =>
            transaction.AccountId == account.Id && transaction.Type == AccountTransactionType.Opening)).Single();

        await _accountAppService.CancelTransactionAsync(opening.Id, new CancelAccountTransactionDto
        {
            Reason = "Hatalı başlangıç tutarı"
        });

        (await _accountRepository.GetAsync(account.Id)).Balance.ShouldBe(0m);
        (await _transactionRepository.GetAsync(opening.Id)).Status.ShouldBe(AccountTransactionStatus.Cancelled);
        var reversal = (await _transactionRepository.GetListAsync(transaction =>
            transaction.ReversalOfTransactionId == opening.Id)).Single();
        reversal.Type.ShouldBe(AccountTransactionType.Reversal);
        reversal.Amount.ShouldBe(-80m);

        await _accountAppService.CreateOpeningBalanceAsync(new CreateAccountOpeningBalanceDto
        {
            AccountId = account.Id,
            Amount = 25m
        });
        (await _accountRepository.GetAsync(account.Id)).Balance.ShouldBe(25m);
    }
}
