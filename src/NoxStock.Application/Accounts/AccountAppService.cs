using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.Permissions;
using NoxStock.Common;
using NoxStock.MultiTenancy;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace NoxStock.Accounts;

[Authorize(NoxStockPermissions.Accounts.Default)]
public class AccountAppService :
    CrudAppService<
        Account,
        AccountDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateAccountDto>,
    IAccountAppService
{
    public AccountAppService(IRepository<Account, Guid> repository)
        : base(repository)
    {
        GetPolicyName = NoxStockPermissions.Accounts.Default;
        GetListPolicyName = NoxStockPermissions.Accounts.Default;
        CreatePolicyName = NoxStockPermissions.Accounts.Create;
        UpdatePolicyName = NoxStockPermissions.Accounts.Edit;
        DeletePolicyName = NoxStockPermissions.Accounts.Delete;
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    [UnitOfWork(true)]
    public override async Task<AccountDto> CreateAsync(CreateUpdateAccountDto input)
    {
        var tenantId = CurrentTenant.RequireTenantId();
        await CheckCreatePolicyAsync();

        var account = new Account(
            GuidGenerator.Create(),
            tenantId,
            input.Name,
            input.Type,
            NormalizeCurrency(input.Currency)
        )
        {
            // The legacy Balance request property is intentionally ignored.
            // New openings must be represented by an AccountTransaction instead.
            Balance = 0m,
            IsActive = input.IsActive
        };

        account = await Repository.InsertAsync(account, autoSave: true);
        return await MapToGetOutputDtoAsync(account);
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    [UnitOfWork(true)]
    public async Task<AccountDto> CreateWithOpeningAsync(CreateAccountWithOpeningDto input)
    {
        var tenantId = CurrentTenant.RequireTenantId();
        await CheckCreatePolicyAsync();
        var operationKey = NormalizeOperationKey(input.OperationKey);

        if (operationKey is not null && input.OpeningAmount > 0m)
        {
            var transactionRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<AccountTransaction, Guid>>();
            var existing = await transactionRepository.FindAsync(transaction =>
                transaction.Type == AccountTransactionType.Opening && transaction.OperationKey == operationKey);
            if (existing is not null)
            {
                return await MapToGetOutputDtoAsync(await Repository.GetAsync(existing.AccountId));
            }
        }

        if (input.OpeningAmount < 0m)
        {
            throw new BusinessException("NoxStock:AccountOpeningAmountMustBePositive");
        }

        if (input.OpeningAmount > 0m && !input.IsActive)
        {
            throw new BusinessException("NoxStock:AccountOpeningInactiveAccount");
        }
        if (input.OpeningAmount > 0m)
        {
            await AuthorizationService.CheckAsync(NoxStockPermissions.Accounts.OpeningBalance);
        }

        var account = new Account(
            GuidGenerator.Create(),
            tenantId,
            input.Name,
            input.Type,
            NormalizeCurrency(input.Currency)
        )
        {
            Balance = 0m,
            IsActive = input.IsActive
        };

        // autoSave flushes the FK target. Both writes remain in this UoW, so a
        // failure in the opening transaction rolls the complete create operation back.
        account = await Repository.InsertAsync(account, autoSave: true);

        if (input.OpeningAmount > 0m)
        {
            await CreateOpeningTransactionAsync(
                account,
                input.OpeningAmount,
                input.OpeningDate,
                input.OpeningDescription,
                operationKey);
        }

        return await MapToGetOutputDtoAsync(account);
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    [UnitOfWork(true)]
    public async Task<AccountTransactionDto> CreateOpeningBalanceAsync(CreateAccountOpeningBalanceDto input)
    {
        CurrentTenant.RequireTenantId();
        await AuthorizationService.CheckAsync(NoxStockPermissions.Accounts.OpeningBalance);

        var operationKey = NormalizeOperationKey(input.OperationKey);
        var transactionRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<AccountTransaction, Guid>>();
        if (operationKey is not null)
        {
            var existing = await transactionRepository.FindAsync(transaction =>
                transaction.Type == AccountTransactionType.Opening && transaction.OperationKey == operationKey);
            if (existing is not null)
            {
                return ObjectMapper.Map<AccountTransaction, AccountTransactionDto>(existing);
            }
        }

        var account = await Repository.GetAsync(input.AccountId);
        var transaction = await CreateOpeningTransactionAsync(
            account,
            input.Amount,
            input.OpeningDate,
            input.Description,
            operationKey);

        return ObjectMapper.Map<AccountTransaction, AccountTransactionDto>(transaction);
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    public override async Task<AccountDto> UpdateAsync(Guid id, CreateUpdateAccountDto input)
    {
        CurrentTenant.RequireTenantId();
        await CheckUpdatePolicyAsync();

        var account = await Repository.GetAsync(id);
        account.Name = input.Name;
        account.Type = input.Type;
        account.Currency = NormalizeCurrency(input.Currency);
        account.IsActive = input.IsActive;

        account = await Repository.UpdateAsync(account, autoSave: true);
        return await MapToGetOutputDtoAsync(account);
    }

    [Authorize(NoxStockPermissions.Accounts.Transfer)]
    [NoxStock.Licensing.TenantLicenseWrite]
    [UnitOfWork(true)]
    public async Task TransferAsync(TransferDto input)
    {
        var tenantId = CurrentTenant.RequireTenantId();
        var operationKey = NormalizeOperationKey(input.OperationKey);
        var transactionRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<AccountTransaction, Guid>>();
        if (operationKey is not null && await transactionRepository.AnyAsync(transaction =>
                transaction.Type == AccountTransactionType.TransferOut && transaction.OperationKey == operationKey))
        {
            return;
        }

        if (input.Amount <= 0m)
        {
            throw new BusinessException("NoxStock:AccountTransferAmountMustBePositive");
        }

        if (input.FromAccountId == input.ToAccountId)
        {
            throw new BusinessException("NoxStock:AccountTransferSameAccount");
        }

        var fromAccount = await Repository.GetAsync(input.FromAccountId);
        var toAccount = await Repository.GetAsync(input.ToAccountId);

        if (!fromAccount.IsActive || !toAccount.IsActive)
        {
            throw new BusinessException("NoxStock:AccountTransferInactiveAccount");
        }

        var fromCurrency = fromAccount.Currency.Trim().ToUpperInvariant();
        var toCurrency = toAccount.Currency.Trim().ToUpperInvariant();
        if (!string.Equals(fromCurrency, toCurrency, StringComparison.Ordinal))
        {
            // A cross-currency transfer needs separate source/target amounts,
            // a rate snapshot and FX gain/loss handling. Until that operation
            // exists, accepting one nominal input amount would corrupt balances.
            throw new BusinessException("NoxStock:AccountTransferCurrencyMismatch")
                .WithData("FromCurrency", fromCurrency)
                .WithData("ToCurrency", toCurrency);
        }

        if (fromAccount.Balance < input.Amount)
        {
            throw new BusinessException("NoxStock:AccountTransferInsufficientBalance")
                .WithData("AvailableAmount", fromAccount.Balance);
        }

        fromAccount.Balance -= input.Amount;
        toAccount.Balance += input.Amount;

        var outgoing = new AccountTransaction(
            GuidGenerator.Create(),
            tenantId,
            fromAccount.Id,
            AccountTransactionType.TransferOut,
            input.Amount,
            fromCurrency,
            input.Date,
            null,
            input.Description ?? $"{toAccount.Name} hesabına transfer",
            operationKey)
        {
            RelatedAccountId = toAccount.Id,
            BalanceAfter = fromAccount.Balance
        };
        await transactionRepository.InsertAsync(outgoing);

        var incoming = new AccountTransaction(
            GuidGenerator.Create(),
            tenantId,
            toAccount.Id,
            AccountTransactionType.TransferIn,
            input.Amount,
            toCurrency,
            input.Date,
            null,
            input.Description ?? $"{fromAccount.Name} hesabından transfer",
            operationKey)
        {
            RelatedAccountId = fromAccount.Id,
            BalanceAfter = toAccount.Balance
        };
        await transactionRepository.InsertAsync(incoming);

        await Repository.UpdateAsync(fromAccount);
        await Repository.UpdateAsync(toAccount);
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    [UnitOfWork(true)]
    public async Task AddTransactionAsync(AddTransactionDto input)
    {
        var tenantId = CurrentTenant.RequireTenantId();
        await AuthorizationService.CheckAsync(NoxStockPermissions.Accounts.AddTransaction);

        if (input.Type is not (AccountTransactionType.Income or AccountTransactionType.Expense))
        {
            throw new BusinessException("NoxStock:AccountTransactionUnsupportedType");
        }

        var operationKey = NormalizeOperationKey(input.OperationKey);
        var transactionRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<AccountTransaction, Guid>>();
        if (operationKey is not null && await transactionRepository.AnyAsync(transaction =>
                transaction.Type == input.Type && transaction.OperationKey == operationKey))
        {
            return;
        }

        var account = await Repository.GetAsync(input.AccountId);
        if (!account.IsActive)
        {
            throw new BusinessException("NoxStock:AccountTransactionInactiveAccount");
        }
        if (input.Amount <= 0m)
        {
            throw new BusinessException("NoxStock:AccountTransactionAmountMustBePositive");
        }
        if ((input.Type is AccountTransactionType.Expense or AccountTransactionType.Payment or AccountTransactionType.TransferOut) &&
            account.Balance < input.Amount)
        {
            throw new BusinessException("NoxStock:AccountTransactionInsufficientBalance")
                .WithData("AvailableAmount", account.Balance);
        }

        if (input.Type == AccountTransactionType.Income || input.Type == AccountTransactionType.Collection)
        {
            account.Balance += input.Amount;
        }
        else if (input.Type == AccountTransactionType.Expense || input.Type == AccountTransactionType.Payment || input.Type == AccountTransactionType.TransferOut)
        {
            account.Balance -= input.Amount;
        }

        var transaction = new AccountTransaction(
            GuidGenerator.Create(),
            tenantId,
            account.Id,
            input.Type,
            input.Amount,
            account.Currency,
            input.Date,
            null,
            input.Description,
            operationKey)
        {
            BalanceAfter = account.Balance
        };
        await transactionRepository.InsertAsync(transaction);

        await Repository.UpdateAsync(account);
    }

    private async Task<AccountTransaction> CreateOpeningTransactionAsync(
        Account account,
        decimal amount,
        DateTime openingDate,
        string? description,
        string? operationKey)
    {
        if (amount <= 0m)
        {
            throw new BusinessException("NoxStock:AccountOpeningAmountMustBePositive");
        }

        if (!account.IsActive)
        {
            throw new BusinessException("NoxStock:AccountOpeningInactiveAccount");
        }

        var transactionRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<AccountTransaction, Guid>>();
        if (await transactionRepository.AnyAsync(transaction =>
                transaction.AccountId == account.Id &&
                transaction.Type == AccountTransactionType.Opening &&
                transaction.Status == AccountTransactionStatus.Active))
        {
            // AccountTransaction has no cancellation state yet. P0.1E will extend
            // this rule so a cancelled opening no longer blocks a replacement one.
            throw new BusinessException("NoxStock:AccountOpeningAlreadyExists");
        }

        var tenantId = CurrentTenant.RequireTenantId();
        var transaction = new AccountTransaction(
            GuidGenerator.Create(),
            tenantId,
            account.Id,
            AccountTransactionType.Opening,
            amount,
            account.Currency.Trim().ToUpperInvariant(),
            openingDate == default ? Clock.Now : openingDate,
            null,
            description,
            operationKey);

        account.Balance += amount;
        transaction.BalanceAfter = account.Balance;
        await transactionRepository.InsertAsync(transaction);
        await Repository.UpdateAsync(account);
        return transaction;
    }

    private static string NormalizeCurrency(string currency)
    {
        return currency.Trim().ToUpperInvariant();
    }

    private static string? NormalizeOperationKey(string? operationKey)
    {
        if (string.IsNullOrWhiteSpace(operationKey))
        {
            return null;
        }

        var normalized = operationKey.Trim();
        if (normalized.Length > 100)
        {
            throw new BusinessException("NoxStock:OperationKeyTooLong");
        }

        return normalized;
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    [UnitOfWork(true)]
    public async Task CancelTransactionAsync(Guid transactionId, CancelAccountTransactionDto input)
    {
        var tenantId = CurrentTenant.RequireTenantId();
        var transactionRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<AccountTransaction, Guid>>();
        var transaction = await transactionRepository.GetAsync(transactionId);

        if (transaction.DocumentId.HasValue || transaction.Type is AccountTransactionType.Collection or AccountTransactionType.Payment)
        {
            throw new BusinessException("NoxStock:AccountTransactionMustBeCancelledFromSource");
        }

        var permission = transaction.Type switch
        {
            AccountTransactionType.Opening => NoxStockPermissions.Accounts.CancelOpeningBalance,
            AccountTransactionType.TransferIn or AccountTransactionType.TransferOut => NoxStockPermissions.Accounts.CancelTransfer,
            _ => NoxStockPermissions.Accounts.CancelTransaction
        };
        await AuthorizationService.CheckAsync(permission);

        if (transaction.Status == AccountTransactionStatus.Cancelled)
        {
            return;
        }

        var originals = (transaction.Type is AccountTransactionType.TransferIn or AccountTransactionType.TransferOut) &&
                        !string.IsNullOrWhiteSpace(transaction.OperationKey)
            ? await transactionRepository.GetListAsync(item =>
                item.OperationKey == transaction.OperationKey &&
                item.Status == AccountTransactionStatus.Active &&
                (item.Type == AccountTransactionType.TransferIn || item.Type == AccountTransactionType.TransferOut))
            : new List<AccountTransaction> { transaction };

        foreach (var original in originals)
        {
            var account = await Repository.GetAsync(original.AccountId);
            var originalEffect = GetBalanceEffect(original);
            var reversalEffect = -originalEffect;
            account.Balance += reversalEffect;

            original.Status = AccountTransactionStatus.Cancelled;
            original.CancelledAt = Clock.Now;
            original.CancelledBy = CurrentUser.Id;
            original.CancellationReason = input.Reason.Trim();

            var reversal = new AccountTransaction(
                GuidGenerator.Create(),
                tenantId,
                account.Id,
                AccountTransactionType.Reversal,
                reversalEffect,
                original.Currency,
                Clock.Now,
                null,
                $"{GetTransactionLabel(original.Type)} iptali: {input.Reason.Trim()}",
                $"cancel:{original.Id:N}")
            {
                RelatedAccountId = original.RelatedAccountId,
                ReversalOfTransactionId = original.Id,
                BalanceAfter = account.Balance
            };

            await Repository.UpdateAsync(account);
            await transactionRepository.UpdateAsync(original);
            await transactionRepository.InsertAsync(reversal);
        }
    }

    private static decimal GetBalanceEffect(AccountTransaction transaction)
    {
        return transaction.Type switch
        {
            AccountTransactionType.Opening or AccountTransactionType.Income or AccountTransactionType.Collection or AccountTransactionType.TransferIn => Math.Abs(transaction.Amount),
            AccountTransactionType.Expense or AccountTransactionType.TransferOut => -Math.Abs(transaction.Amount),
            AccountTransactionType.Payment => transaction.Amount < 0m ? transaction.Amount : -transaction.Amount,
            AccountTransactionType.Reversal => transaction.Amount,
            _ => transaction.Amount
        };
    }

    private static string GetTransactionLabel(AccountTransactionType type)
    {
        return type switch
        {
            AccountTransactionType.Opening => "Açılış bakiyesi",
            AccountTransactionType.Income => "Para girişi",
            AccountTransactionType.Expense => "Para çıkışı",
            AccountTransactionType.TransferIn or AccountTransactionType.TransferOut => "Hesaplar arası transfer",
            AccountTransactionType.Collection => "Tahsilat",
            AccountTransactionType.Payment => "Ödeme",
            _ => "Hesap hareketi"
        };
    }

    public async Task<PagedResultDto<AccountTransactionDto>> GetTransactionsAsync(Guid accountId, PagedAndSortedResultRequestDto input)
    {
        CurrentTenant.RequireTenantId();
        await CheckGetPolicyAsync();
        var transactionRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<AccountTransaction, Guid>>();
        
        var query = await transactionRepository.GetQueryableAsync();
        query = query.Where(t => t.AccountId == accountId);

        var totalCount = await AsyncExecuter.CountAsync(query);

        var transactions = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(t => t.TransactionDate)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount)
        );

        return new PagedResultDto<AccountTransactionDto>(
            totalCount,
            ObjectMapper.Map<System.Collections.Generic.List<AccountTransaction>, System.Collections.Generic.List<AccountTransactionDto>>(transactions)
        );
    }

    public override Task<AccountDto> GetAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        return base.GetAsync(id);
    }

    public override async Task<PagedResultDto<AccountDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        CurrentTenant.RequireTenantId();
        input.SkipCount = Math.Clamp(input.SkipCount, 0, 10_000);
        input.MaxResultCount = Math.Clamp(input.MaxResultCount, 1, 100);
        var result = await base.GetListAsync(input);
        if (result.Items.Count == 0)
        {
            return result;
        }

        var accountIds = result.Items.Select(item => item.Id).ToList();
        var today = Clock.Now.Date;
        var tomorrow = today.AddDays(1);
        var transactionRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<AccountTransaction, Guid>>();
        var transactions = await transactionRepository.GetListAsync(transaction =>
            accountIds.Contains(transaction.AccountId) && transaction.Status == AccountTransactionStatus.Active);

        foreach (var account in result.Items)
        {
            var accountTransactions = transactions.Where(transaction => transaction.AccountId == account.Id).ToList();
            account.LastTransactionDate = accountTransactions.Count == 0
                ? null
                : accountTransactions.Max(transaction => transaction.TransactionDate);
            account.TodayIncome = accountTransactions
                .Where(transaction => transaction.TransactionDate >= today && transaction.TransactionDate < tomorrow)
                .Sum(transaction => Math.Max(0m, GetBalanceEffect(transaction)));
            account.TodayExpense = Math.Abs(accountTransactions
                .Where(transaction => transaction.TransactionDate >= today && transaction.TransactionDate < tomorrow)
                .Sum(transaction => Math.Min(0m, GetBalanceEffect(transaction))));
        }

        return result;
    }

    public async Task<PagedResultDto<AccountLookupDto>> GetLookupListAsync(AccountLookupInput input)
    {
        CurrentTenant.RequireTenantId();
        await CheckGetListPolicyAsync();

        var filter = SearchText.Normalize(input.Filter);
        var currency = input.Currency?.Trim().ToUpperInvariant();
        var query = (await Repository.GetQueryableAsync())
            .Where(account => account.IsActive)
            .WhereIf(!string.IsNullOrWhiteSpace(currency), account => account.Currency == currency)
            .WhereIf(!string.IsNullOrWhiteSpace(filter), account => account.Name.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(filter));

        var totalCount = await AsyncExecuter.CountAsync(query);
        var maxResultCount = Math.Clamp(input.MaxResultCount, 1, 30);
        var rows = await AsyncExecuter.ToListAsync(query
            .OrderBy(account => account.Name)
            .Skip(Math.Max(input.SkipCount, 0))
            .Take(maxResultCount)
            .Select(account => new AccountLookupDto
            {
                Id = account.Id,
                Name = account.Name,
                Type = account.Type,
                Currency = account.Currency,
                Balance = account.Balance
            }));

        return new PagedResultDto<AccountLookupDto>(totalCount, rows);
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    public override Task DeleteAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        return base.DeleteAsync(id);
    }
}
