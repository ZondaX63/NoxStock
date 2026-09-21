using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NoxStock.Accounts;
using NoxStock.Documents;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace NoxStock.EntityFrameworkCore;

// PostgreSQL-specific row locks are kept in persistence instead of application workflows.
public sealed class PaymentAllocationLockStore : IPaymentAllocationLockStore, ITransientDependency
{
    private readonly IDbContextProvider<NoxStockDbContext> _dbContextProvider;

    public PaymentAllocationLockStore(IDbContextProvider<NoxStockDbContext> dbContextProvider)
    {
        _dbContextProvider = dbContextProvider;
    }

    public async Task LockAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accountTransactionIds,
        IReadOnlyCollection<Guid> documentIds)
    {
        var db = await _dbContextProvider.GetDbContextAsync();
        if (!db.Database.IsNpgsql())
        {
            // SQLite workflow tests intentionally do not claim PostgreSQL locking semantics.
            return;
        }

        var transactionIds = accountTransactionIds.Distinct().OrderBy(x => x).ToArray();
        if (transactionIds.Length > 0)
        {
            await db.AccountTransactions.FromSqlInterpolated($"SELECT * FROM \"AppAccountTransactions\" WHERE \"TenantId\" = {tenantId} AND \"Id\" = ANY({transactionIds}) ORDER BY \"Id\" FOR UPDATE")
                .AsNoTracking()
                .ToListAsync();
        }

        var documentIdsOrdered = documentIds.Distinct().OrderBy(x => x).ToArray();
        if (documentIdsOrdered.Length > 0)
        {
            await db.Documents.FromSqlInterpolated($"SELECT * FROM \"AppDocuments\" WHERE \"TenantId\" = {tenantId} AND \"Id\" = ANY({documentIdsOrdered}) ORDER BY \"Id\" FOR UPDATE")
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
