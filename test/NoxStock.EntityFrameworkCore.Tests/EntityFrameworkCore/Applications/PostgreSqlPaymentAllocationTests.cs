using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NoxStock.Accounts;
using NoxStock.BusinessPartners;
using NoxStock.Documents;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace NoxStock.EntityFrameworkCore.Applications;

/// <summary>
/// PostgreSQL acceptance tests for the allocation aggregate.  Each assertion reads
/// through a new UoW/DbContext; the concurrent cases exercise the provider-backed
/// FOR UPDATE lock store rather than SQLite's no-op implementation.
/// </summary>
public class PostgreSqlPaymentAllocationTests : NoxStockEntityFrameworkCoreTestBase
{
    [PostgreSqlFact]
    public async Task AddPaymentAllocationOperationKey_Migration_Applies_And_Rolls_Back_On_Empty_PostgreSql_Database()
    {
        var databaseName = $"noxstock_allocation_operation_key_{Guid.NewGuid():N}";
        var admin = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NOXSTOCK_POSTGRES_TEST_CONNECTION")) { Database = "postgres" };
        await using (var connection = new NpgsqlConnection(admin.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        var target = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = databaseName };
        try
        {
            var options = new DbContextOptionsBuilder<NoxStockDbContext>().UseNpgsql(target.ConnectionString).Options;
            await using var db = new NoxStockDbContext(options);
            await db.Database.MigrateAsync();
            (await HasOperationKeyColumnAsync(target.ConnectionString)).ShouldBeTrue();
            await db.GetService<IMigrator>().MigrateAsync("20260922124711_AddTenantCloneOperations");
            (await HasOperationKeyColumnAsync(target.ConnectionString)).ShouldBeFalse();
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(admin.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    [PostgreSqlFact]
    public async Task Same_Batch_Operation_Key_Concurrently_Replays_One_Allocation_Set()
    {
        var data = await SeedAsync(documentTotals: [100m], transactionAmounts: [100m]);
        var input = Batch(data.Transactions[0], [(data.Documents[0], 100m)], "pg-allocation-same-key");

        var results = await Task.WhenAll(AllocateInNewScopeAsync(input), AllocateInNewScopeAsync(input));

        results.All(x => x.ErrorCode is null || x.ErrorCode == "NoxStock:CommandInProgress").ShouldBeTrue();
        var replay = await AllocateInNewScopeAsync(input);
        replay.ErrorCode.ShouldBeNull();
        replay.Result!.Count.ShouldBe(1);
        var state = await ReadAsync(data);
        state.Allocations.Count.ShouldBe(1);
        replay.Result.Single().Id.ShouldBe(state.Allocations.Single().Id);
        state.Allocations.Single().Status.ShouldBe(PaymentAllocationStatus.Active);
        state.Commands.Count(x => x.Type == CommercialCommandType.PaymentAllocationBatch).ShouldBe(1);
        state.Commands.Single(x => x.Type == CommercialCommandType.PaymentAllocationBatch).Status.ShouldBe(CommercialCommandStatus.Succeeded);
    }

    [PostgreSqlFact]
    public async Task Same_Reversal_Operation_Key_Concurrently_Leaves_One_Reversal_Effect()
    {
        var data = await SeedAsync(documentTotals: [100m], transactionAmounts: [100m]);
        var allocation = (await AllocateInNewScopeAsync(Batch(data.Transactions[0], [(data.Documents[0], 100m)], "pg-create-for-reverse"))).Result!.Single();

        var results = await Task.WhenAll(
            ReverseInNewScopeAsync(allocation.Id, "pg-reversal-same-key"),
            ReverseInNewScopeAsync(allocation.Id, "pg-reversal-same-key"));

        results.All(x => x is null || x == "NoxStock:CommandInProgress").ShouldBeTrue();
        (await ReverseInNewScopeAsync(allocation.Id, "pg-reversal-same-key")).ShouldBeNull();
        var state = await ReadAsync(data);
        state.Allocations.Single().Status.ShouldBe(PaymentAllocationStatus.Reversed);
        state.Documents.Single().PaidAmount.ShouldBe(0m);
        state.Documents.Single().Status.ShouldBe(DocumentStatus.Approved);
        state.Commands.Count(x => x.Type == CommercialCommandType.PaymentAllocationReversal).ShouldBe(1);
    }

    [PostgreSqlFact]
    public async Task Same_Key_With_Different_Payload_Is_Rejected_Without_Second_Allocation()
    {
        var data = await SeedAsync(documentTotals: [100m], transactionAmounts: [100m]);
        var first = await AllocateInNewScopeAsync(Batch(data.Transactions[0], [(data.Documents[0], 40m)], "pg-payload-key"));
        first.ErrorCode.ShouldBeNull();

        var changed = await AllocateInNewScopeAsync(Batch(data.Transactions[0], [(data.Documents[0], 50m)], "pg-payload-key"));

        changed.ErrorCode.ShouldBe("NoxStock:OperationKeyAlreadyUsed");
        var state = await ReadAsync(data);
        state.Allocations.Count.ShouldBe(1);
        state.Allocations.Single().Amount.ShouldBe(40m);
    }

    [PostgreSqlFact]
    public async Task Reversal_Key_Cannot_Be_Reused_For_A_Different_Allocation()
    {
        var data = await SeedAsync(documentTotals: [100m, 100m], transactionAmounts: [100m, 100m]);
        var first = (await AllocateInNewScopeAsync(Batch(data.Transactions[0], [(data.Documents[0], 50m)], "pg-reverse-payload-a"))).Result!.Single();
        var second = (await AllocateInNewScopeAsync(Batch(data.Transactions[1], [(data.Documents[1], 50m)], "pg-reverse-payload-b"))).Result!.Single();

        (await ReverseInNewScopeAsync(first.Id, "pg-reversal-payload-key")).ShouldBeNull();
        (await ReverseInNewScopeAsync(second.Id, "pg-reversal-payload-key")).ShouldBe("NoxStock:OperationKeyAlreadyUsed");

        var state = await ReadAsync(data);
        state.Allocations.Single(x => x.Id == first.Id).Status.ShouldBe(PaymentAllocationStatus.Reversed);
        state.Allocations.Single(x => x.Id == second.Id).Status.ShouldBe(PaymentAllocationStatus.Active);
    }

    [PostgreSqlFact]
    public async Task Concurrent_Different_Payments_Cannot_OverAllocate_One_Invoice()
    {
        var data = await SeedAsync(documentTotals: [100m], transactionAmounts: [75m, 75m]);
        var attempts = await Task.WhenAll(
            AllocateInNewScopeAsync(Batch(data.Transactions[0], [(data.Documents[0], 75m)], "pg-doc-race-a")),
            AllocateInNewScopeAsync(Batch(data.Transactions[1], [(data.Documents[0], 75m)], "pg-doc-race-b")));

        attempts.Count(x => x.ErrorCode is null).ShouldBe(1);
        var state = await ReadAsync(data);
        state.Allocations.Where(x => x.Status == PaymentAllocationStatus.Active).Sum(x => x.Amount).ShouldBeLessThanOrEqualTo(100m);
        state.Documents.Single().PaidAmount.ShouldBe(75m);
    }

    [PostgreSqlFact]
    public async Task Concurrent_Batches_Cannot_OverAllocate_The_Same_Payment_And_Keep_Only_One_Active_Source_Document_Pair()
    {
        var data = await SeedAsync(documentTotals: [75m, 75m], transactionAmounts: [100m]);
        var attempts = await Task.WhenAll(
            AllocateInNewScopeAsync(Batch(data.Transactions[0], [(data.Documents[0], 75m)], "pg-source-race-a")),
            AllocateInNewScopeAsync(Batch(data.Transactions[0], [(data.Documents[1], 75m)], "pg-source-race-b")));

        attempts.Count(x => x.ErrorCode is null).ShouldBe(1);
        var state = await ReadAsync(data);
        state.Allocations.Where(x => x.Status == PaymentAllocationStatus.Active).Sum(x => x.Amount).ShouldBeLessThanOrEqualTo(100m);

        var duplicate = await AllocateInNewScopeAsync(Batch(data.Transactions[0], [(data.Documents[0], 10m)], "pg-source-document-duplicate"));
        duplicate.ErrorCode.ShouldBeOneOf("NoxStock:DuplicatePaymentAllocation", "NoxStock:PaymentAllocationExceedsSourceAmount");
        (await ReadAsync(data)).Allocations.Count.ShouldBe(1);
    }

    [PostgreSqlFact]
    public async Task Invalid_Second_Batch_Line_Rolls_Back_Allocation_And_Document_State()
    {
        var data = await SeedAsync(documentTotals: [100m, 100m], transactionAmounts: [100m]);
        var result = await AllocateInNewScopeAsync(Batch(data.Transactions[0], [(data.Documents[0], 20m), (data.Documents[1], 90m)], "pg-batch-rollback"));

        result.ErrorCode.ShouldBe("NoxStock:PaymentAllocationExceedsSourceAmount");
        var state = await ReadAsync(data);
        state.Allocations.ShouldBeEmpty();
        state.Documents.All(x => x.PaidAmount == 0m && x.LegacyPaidAmountBaseline is null && x.Status == DocumentStatus.Approved).ShouldBeTrue();
        state.Partner.Balance.ShouldBe(0m);
        state.Account.Balance.ShouldBe(0m);
    }

    [PostgreSqlFact]
    public async Task Tenant_Partner_And_Currency_Mismatches_Leave_Persistent_State_Unchanged()
    {
        var data = await SeedAsync(documentTotals: [100m], transactionAmounts: [100m]);
        var otherPartner = await AddPartnerAsync(data.TenantId, "Other partner", "TRY");
        var mismatch = await AddDocumentAsync(data.TenantId, otherPartner, 100m, "TRY");
        var result = await AllocateInNewScopeAsync(Batch(data.Transactions[0], [(mismatch, 20m)], "pg-partner-mismatch"), data.TenantId);
        result.ErrorCode.ShouldBe("NoxStock:PaymentAllocationBusinessPartnerMismatch");

        var state = await ReadAsync(data);
        state.Allocations.ShouldBeEmpty();
        state.Documents.Single(x => x.Id == data.Documents[0]).PaidAmount.ShouldBe(0m);
        (await ReadDocumentAsync(mismatch, data.TenantId)).PaidAmount.ShouldBe(0m);

        var currencyMismatch = await AddDocumentAsync(data.TenantId, data.PartnerId, 100m, "USD");
        var currency = await AllocateInNewScopeAsync(Batch(data.Transactions[0], [(currencyMismatch, 20m)], "pg-currency-mismatch"), data.TenantId);
        currency.ErrorCode.ShouldBe("NoxStock:PaymentAllocationCurrencyMismatch");
        (await ReadAsync(data)).Allocations.ShouldBeEmpty();

        var tenantB = Guid.NewGuid();
        var otherTenant = await SeedAsync(documentTotals: [100m], transactionAmounts: [100m], tenantId: tenantB);
        var isolated = await AllocateInNewScopeAsync(Batch(otherTenant.Transactions[0], [(otherTenant.Documents[0], 20m)], "pg-tenant-mismatch"), data.TenantId);
        isolated.ErrorCode.ShouldNotBeNull();
        (await ReadAsync(data)).Allocations.ShouldBeEmpty();
        (await ReadAsync(otherTenant)).Allocations.ShouldBeEmpty();
    }

    [PostgreSqlFact]
    public async Task Reversal_Recalculates_And_Allows_A_New_Active_Source_Document_Allocation()
    {
        var data = await SeedAsync(documentTotals: [100m], transactionAmounts: [100m]);
        var allocation = (await AllocateInNewScopeAsync(Batch(data.Transactions[0], [(data.Documents[0], 50m)], "pg-reallocate-before"))).Result!.Single();
        (await ReverseInNewScopeAsync(allocation.Id, "pg-reallocate-reverse")).ShouldBeNull();
        var second = await AllocateInNewScopeAsync(Batch(data.Transactions[0], [(data.Documents[0], 50m)], "pg-reallocate-after"));

        second.ErrorCode.ShouldBeNull();
        var state = await ReadAsync(data);
        state.Allocations.Count.ShouldBe(2);
        state.Allocations.Count(x => x.Status == PaymentAllocationStatus.Reversed).ShouldBe(1);
        state.Allocations.Count(x => x.Status == PaymentAllocationStatus.Active).ShouldBe(1);
        state.Documents.Single().PaidAmount.ShouldBe(50m);
        state.Documents.Single().Status.ShouldBe(DocumentStatus.PartiallyPaid);
    }

    [PostgreSqlFact]
    public async Task Legacy_Baseline_Is_Captured_Once_During_Concurrent_First_Allocations()
    {
        var data = await SeedAsync(documentTotals: [100m], transactionAmounts: [20m, 20m], initialPaidAmount: 20m);
        var attempts = await Task.WhenAll(
            AllocateInNewScopeAsync(Batch(data.Transactions[0], [(data.Documents[0], 20m)], "pg-legacy-a")),
            AllocateInNewScopeAsync(Batch(data.Transactions[1], [(data.Documents[0], 20m)], "pg-legacy-b")));

        attempts.All(x => x.ErrorCode is null).ShouldBeTrue();
        var state = await ReadAsync(data);
        state.Documents.Single().LegacyPaidAmountBaseline.ShouldBe(20m);
        state.Documents.Single().PaidAmount.ShouldBe(60m);
        state.Allocations.Where(x => x.Status == PaymentAllocationStatus.Active).Sum(x => x.Amount).ShouldBe(40m);
    }

    [PostgreSqlFact]
    public async Task Payment_Cancellation_Reverses_Linked_Allocations_And_Restores_Invoice_Balance()
    {
        var data = await SeedAsync(documentTotals: [100m], transactionAmounts: [50m], accountBalance: 50m);
        var allocation = await AllocateInNewScopeAsync(Batch(data.Transactions[0], [(data.Documents[0], 50m)], "pg-cancel-payment-allocation"));
        allocation.ErrorCode.ShouldBeNull();

        using (var scope = ServiceProvider.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(data.TenantId))
        {
            await scope.ServiceProvider.GetRequiredService<IBusinessPartnerAppService>().CancelPaymentAsync(
                data.Transactions[0], new CancelBusinessPartnerPaymentDto { Reason = "PostgreSQL proof" });
        }

        var state = await ReadAsync(data);
        state.Allocations.Single().Status.ShouldBe(PaymentAllocationStatus.Reversed);
        state.Documents.Single().PaidAmount.ShouldBe(0m);
        state.Documents.Single().Status.ShouldBe(DocumentStatus.Approved);
        state.Transactions.Single().Status.ShouldBe(AccountTransactionStatus.Cancelled);
    }

    private async Task<Seed> SeedAsync(decimal[] documentTotals, decimal[] transactionAmounts, decimal initialPaidAmount = 0m, decimal accountBalance = 0m, Guid? tenantId = null)
    {
        var effectiveTenantId = tenantId ?? NoxStockTestConsts.TenantId;
        using var scope = ServiceProvider.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(effectiveTenantId, "NoxStock Test Tenant");
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
        var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<NoxStockDbContext>>().GetDbContextAsync();
        var partner = new BusinessPartner(Guid.NewGuid(), effectiveTenantId, "PG Allocation Partner " + Guid.NewGuid().ToString("N"), BusinessPartnerType.Customer) { Balance = 0m };
        var account = new Account(Guid.NewGuid(), effectiveTenantId, "PG Allocation Cash " + Guid.NewGuid().ToString("N"), AccountType.Cash) { Currency = "TRY", Balance = accountBalance };
        db.AddRange(partner, account);
        var documents = documentTotals.Select((total, index) => new Document(Guid.NewGuid(), effectiveTenantId)
        {
            BusinessPartnerId = partner.Id,
            DocNumber = $"PG-ALLOC-{Guid.NewGuid():N}-{index}",
            Type = DocumentType.SaleInvoice,
            Status = DocumentStatus.Approved,
            GrandTotal = total,
            Subtotal = total,
            Currency = "TRY",
            ExchangeRate = 1m,
            Date = DateTime.UtcNow,
            PaidAmount = initialPaidAmount
        }).ToArray();
        var transactions = transactionAmounts.Select(amount =>
        {
            var transaction = new AccountTransaction(Guid.NewGuid(), effectiveTenantId, account.Id, AccountTransactionType.Collection, amount, "TRY", DateTime.UtcNow)
            { BusinessPartnerId = partner.Id, BalanceAfter = account.Balance };
            return transaction;
        }).ToArray();
        db.AddRange(documents); db.AddRange(transactions);
        await db.SaveChangesAsync();
        await uow.CompleteAsync();
        return new Seed(effectiveTenantId, partner.Id, account.Id, documents.Select(x => x.Id).ToArray(), transactions.Select(x => x.Id).ToArray());
    }

    private async Task<Guid> AddPartnerAsync(Guid tenantId, string name, string currency)
    {
        using var scope = ServiceProvider.CreateScope(); using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
        var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<NoxStockDbContext>>().GetDbContextAsync();
        var partner = new BusinessPartner(Guid.NewGuid(), tenantId, name, BusinessPartnerType.Customer, currency); db.BusinessPartners.Add(partner);
        await db.SaveChangesAsync(); await uow.CompleteAsync(); return partner.Id;
    }

    private async Task<Guid> AddDocumentAsync(Guid tenantId, Guid partnerId, decimal total, string currency)
    {
        using var scope = ServiceProvider.CreateScope(); using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
        var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<NoxStockDbContext>>().GetDbContextAsync();
        var document = new Document(Guid.NewGuid(), tenantId) { BusinessPartnerId = partnerId, DocNumber = "PG-MISMATCH-" + Guid.NewGuid().ToString("N"), Type = DocumentType.SaleInvoice, Status = DocumentStatus.Approved, GrandTotal = total, Subtotal = total, Currency = currency, ExchangeRate = 1m, Date = DateTime.UtcNow };
        db.Documents.Add(document); await db.SaveChangesAsync(); await uow.CompleteAsync(); return document.Id;
    }

    private static CreatePaymentAllocationBatchDto Batch(Guid transactionId, IEnumerable<(Guid DocumentId, decimal Amount)> lines, string key) => new()
    {
        AccountTransactionId = transactionId, OperationKey = key,
        Allocations = lines.Select(x => new CreatePaymentAllocationLineDto { DocumentId = x.DocumentId, Amount = x.Amount, AllocationDate = DateTime.UtcNow }).ToList()
    };

    private async Task<AllocationAttempt> AllocateInNewScopeAsync(CreatePaymentAllocationBatchDto input, Guid? tenantId = null)
    {
        using var scope = ServiceProvider.CreateScope(); using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId ?? NoxStockTestConsts.TenantId);
        try { return new AllocationAttempt(await scope.ServiceProvider.GetRequiredService<IPaymentAllocationAppService>().CreateBatchAsync(input), null); }
        catch (BusinessException exception) { return new AllocationAttempt(null, exception.Code); }
        catch (EntityNotFoundException) { return new AllocationAttempt(null, "NoxStock:EntityNotFound"); }
    }

    private async Task<string?> ReverseInNewScopeAsync(Guid allocationId, string operationKey)
    {
        using var scope = ServiceProvider.CreateScope(); using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(NoxStockTestConsts.TenantId);
        try { await scope.ServiceProvider.GetRequiredService<IPaymentAllocationAppService>().ReverseAsync(allocationId, new ReversePaymentAllocationDto { OperationKey = operationKey }); return null; }
        catch (BusinessException exception) { return exception.Code; }
    }

    private async Task<State> ReadAsync(Seed data)
    {
        using var scope = ServiceProvider.CreateScope(); using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(data.TenantId);
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
        var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<NoxStockDbContext>>().GetDbContextAsync();
        var state = new State(
            await db.PaymentAllocations.Where(x => x.TenantId == data.TenantId).AsNoTracking().ToListAsync(),
            await db.Documents.Where(x => data.Documents.Contains(x.Id)).AsNoTracking().ToListAsync(),
            await db.CommercialCommands.Where(x => x.TenantId == data.TenantId).AsNoTracking().ToListAsync(),
            await db.AccountTransactions.Where(x => data.Transactions.Contains(x.Id)).AsNoTracking().ToListAsync(),
            await db.BusinessPartners.SingleAsync(x => x.Id == data.PartnerId),
            await db.Accounts.SingleAsync(x => x.Id == data.AccountId));
        await uow.CompleteAsync(); return state;
    }

    private async Task<Document> ReadDocumentAsync(Guid id, Guid tenantId)
    {
        using var scope = ServiceProvider.CreateScope(); using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
        var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<NoxStockDbContext>>().GetDbContextAsync();
        var result = await db.Documents.AsNoTracking().SingleAsync(x => x.Id == id); await uow.CompleteAsync(); return result;
    }

    private static async Task<bool> HasOperationKeyColumnAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'AppPaymentAllocations' AND column_name = 'OperationKey')
            """, connection);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private sealed record Seed(Guid TenantId, Guid PartnerId, Guid AccountId, Guid[] Documents, Guid[] Transactions);
    private sealed record AllocationAttempt(List<PaymentAllocationDto>? Result, string? ErrorCode);
    private sealed record State(List<PaymentAllocation> Allocations, List<Document> Documents, List<CommercialCommand> Commands, List<AccountTransaction> Transactions, BusinessPartner Partner, Account Account);
}
