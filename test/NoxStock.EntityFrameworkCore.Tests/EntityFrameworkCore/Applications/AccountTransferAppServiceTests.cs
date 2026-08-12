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

public class AccountTransferAppServiceTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly IAccountAppService _accountAppService;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<AccountTransaction, Guid> _transactionRepository;

    public AccountTransferAppServiceTests()
    {
        _accountAppService = ServiceProvider.GetRequiredService<IAccountAppService>();
        _accountRepository = ServiceProvider.GetRequiredService<IRepository<Account, Guid>>();
        _transactionRepository = ServiceProvider.GetRequiredService<IRepository<AccountTransaction, Guid>>();
    }

    [Fact]
    public async Task Should_Reject_Cross_Currency_Transfer_Without_Changing_Balances()
    {
        var cash = await CreateAccountAsync("TRY Kasa", "TRY", 1_000m);
        var euroBank = await CreateAccountAsync("EUR Banka", "EUR", 100m);

        var exception = await Should.ThrowAsync<BusinessException>(() => _accountAppService.TransferAsync(new TransferDto
        {
            FromAccountId = cash.Id,
            ToAccountId = euroBank.Id,
            Amount = 100m,
            Date = DateTime.UtcNow
        }));

        exception.Code.ShouldBe("NoxStock:AccountTransferCurrencyMismatch");
        (await _accountRepository.GetAsync(cash.Id)).Balance.ShouldBe(1_000m);
        (await _accountRepository.GetAsync(euroBank.Id)).Balance.ShouldBe(100m);
        (await _transactionRepository.GetListAsync(transaction =>
            transaction.AccountId == cash.Id || transaction.AccountId == euroBank.Id))
            .Count.ShouldBe(2); // Only the two opening movements exist.
    }

    [Fact]
    public async Task Should_Transfer_Between_Active_Accounts_With_The_Same_Currency()
    {
        var source = await CreateAccountAsync("TRY Kasa", "TRY", 1_000m);
        var target = await CreateAccountAsync("TRY Banka", "TRY", 100m);

        await _accountAppService.TransferAsync(new TransferDto
        {
            FromAccountId = source.Id,
            ToAccountId = target.Id,
            Amount = 250m,
            Date = DateTime.UtcNow,
            Description = "Gün sonu virmanı"
        });

        (await _accountRepository.GetAsync(source.Id)).Balance.ShouldBe(750m);
        (await _accountRepository.GetAsync(target.Id)).Balance.ShouldBe(350m);
        var transactions = await _transactionRepository.GetListAsync(transaction =>
            (transaction.AccountId == source.Id || transaction.AccountId == target.Id) &&
            transaction.Type != AccountTransactionType.Opening);
        transactions.Count.ShouldBe(2);
        transactions.Single(x => x.AccountId == source.Id).Type.ShouldBe(AccountTransactionType.TransferOut);
        transactions.Single(x => x.AccountId == source.Id).Amount.ShouldBe(250m);
        transactions.Single(x => x.AccountId == target.Id).Type.ShouldBe(AccountTransactionType.TransferIn);
        transactions.Single(x => x.AccountId == target.Id).Amount.ShouldBe(250m);
    }

    [Fact]
    public async Task Should_Not_Duplicate_And_Should_Cancel_Transfer_With_Reversals()
    {
        var source = await CreateAccountAsync("Virman kaynak", "TRY", 500m);
        var target = await CreateAccountAsync("Virman hedef", "TRY", 50m);
        var input = new TransferDto
        {
            FromAccountId = source.Id,
            ToAccountId = target.Id,
            Amount = 100m,
            Date = DateTime.UtcNow,
            OperationKey = "transfer-retry-test"
        };

        await _accountAppService.TransferAsync(input);
        await _accountAppService.TransferAsync(input);

        (await _accountRepository.GetAsync(source.Id)).Balance.ShouldBe(400m);
        (await _accountRepository.GetAsync(target.Id)).Balance.ShouldBe(150m);
        var transferRows = await _transactionRepository.GetListAsync(transaction =>
            transaction.OperationKey == input.OperationKey);
        transferRows.Count.ShouldBe(2);

        await _accountAppService.CancelTransactionAsync(
            transferRows.Single(transaction => transaction.Type == AccountTransactionType.TransferOut).Id,
            new CancelAccountTransactionDto { Reason = "Yanlış hedef hesap" });

        (await _accountRepository.GetAsync(source.Id)).Balance.ShouldBe(500m);
        (await _accountRepository.GetAsync(target.Id)).Balance.ShouldBe(50m);
        (await _transactionRepository.GetListAsync(transaction =>
            transaction.ReversalOfTransactionId.HasValue)).Count.ShouldBe(2);
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
