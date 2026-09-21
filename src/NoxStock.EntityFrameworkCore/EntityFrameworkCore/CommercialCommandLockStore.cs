using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NoxStock.Documents;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace NoxStock.EntityFrameworkCore;

// Provider-specific locking is deliberately contained in persistence.
public class CommercialCommandLockStore : ICommercialCommandLockStore, ITransientDependency
{
    private readonly IDbContextProvider<NoxStockDbContext> _dbContextProvider;
    public CommercialCommandLockStore(IDbContextProvider<NoxStockDbContext> dbContextProvider) => _dbContextProvider = dbContextProvider;

    public async Task<CommercialCommand?> LockForExecutionAsync(Guid id)
    {
        var db = await _dbContextProvider.GetDbContextAsync();
        return await db.CommercialCommands.FromSqlInterpolated($"SELECT * FROM \"AppCommercialCommands\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync();
    }

    public async Task<List<CommercialCommand>> LockStaleAsync(DateTime now, int maxCount)
    {
        var db = await _dbContextProvider.GetDbContextAsync();
        return await db.CommercialCommands.FromSqlInterpolated($"SELECT * FROM \"AppCommercialCommands\" WHERE \"Status\" = {(int)CommercialCommandStatus.Processing} AND \"LeaseExpiresAt\" IS NOT NULL AND \"LeaseExpiresAt\" <= {now} ORDER BY \"LeaseExpiresAt\" FOR UPDATE SKIP LOCKED LIMIT {maxCount}")
            .ToListAsync();
    }

    public async Task<bool> TryCompleteAsync(Guid id, Guid? tenantId, Guid executionToken, CommercialCommandStatus status, DateTime completedAt, string? failureCode)
    {
        var db = await _dbContextProvider.GetDbContextAsync();
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"AppCommercialCommands\" SET \"Status\" = {(int)status}, \"CompletedAt\" = {completedAt}, \"FailureCode\" = {failureCode} WHERE \"Id\" = {id} AND \"TenantId\" = {tenantId} AND \"Status\" = {(int)CommercialCommandStatus.Processing} AND \"ExecutionToken\" = {executionToken}");
        return affected == 1;
    }

    public async Task<bool> TryExpireAsync(Guid id, Guid? tenantId, Guid executionToken, DateTime now)
    {
        var db = await _dbContextProvider.GetDbContextAsync();
        const string failureCode = "NoxStock:CommandExpired";
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"AppCommercialCommands\" SET \"Status\" = {(int)CommercialCommandStatus.Expired}, \"CompletedAt\" = {now}, \"FailureCode\" = {failureCode} WHERE \"Id\" = {id} AND \"TenantId\" = {tenantId} AND \"Status\" = {(int)CommercialCommandStatus.Processing} AND \"ExecutionToken\" = {executionToken} AND \"LeaseExpiresAt\" IS NOT NULL AND \"LeaseExpiresAt\" < {now}");
        return affected == 1;
    }
}
