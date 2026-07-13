using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace NexStock.EntityFrameworkCore.MultiTenancy;

public sealed record NullTenantTableAudit(string Table, long RowCount, IReadOnlyList<Guid> SampleIds);

public class NullTenantDataAuditService : ITransientDependency
{
    private readonly IDbContextProvider<NexStockDbContext> _dbContextProvider;
    private readonly ILogger<NullTenantDataAuditService> _logger;

    public NullTenantDataAuditService(
        IDbContextProvider<NexStockDbContext> dbContextProvider,
        ILogger<NullTenantDataAuditService> logger)
    {
        _dbContextProvider = dbContextProvider;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task<IReadOnlyList<NullTenantTableAudit>> ScanAsync()
    {
        var dbContext = await _dbContextProvider.GetDbContextAsync();
        var results = new List<NullTenantTableAudit>();

        await AddAuditAsync(results, "AppBrands", dbContext.Brands);
        await AddAuditAsync(results, "AppCategories", dbContext.Categories);
        await AddAuditAsync(results, "AppProducts", dbContext.Products);
        await AddAuditAsync(results, "AppProductOEMCodes", dbContext.ProductOEMCodes);
        await AddAuditAsync(results, "AppBusinessPartners", dbContext.BusinessPartners);
        await AddAuditAsync(results, "AppDocuments", dbContext.Documents);
        await AddAuditAsync(results, "AppDocumentItems", dbContext.DocumentItems);
        await AddAuditAsync(results, "AppAccounts", dbContext.Accounts);
        await AddAuditAsync(results, "AppAccountTransactions", dbContext.AccountTransactions);
        await AddAuditAsync(results, "AppStockMovements", dbContext.StockMovements);

        foreach (var result in results.Where(x => x.RowCount > 0))
        {
            _logger.LogWarning(
                "NULL_TENANT_DATA table={Table} count={Count} sampleIds={SampleIds}. Manual ownership mapping is required; no data was changed.",
                result.Table,
                result.RowCount,
                string.Join(',', result.SampleIds));
        }

        return results;
    }

    private static async Task AddAuditAsync<TEntity>(
        ICollection<NullTenantTableAudit> results,
        string table,
        DbSet<TEntity> set)
        where TEntity : class, IEntity<Guid>, IMultiTenant
    {
        var query = set.IgnoreQueryFilters().Where(x => x.TenantId == null);
        var count = await query.LongCountAsync();
        var sampleIds = await query
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Take(10)
            .ToListAsync();

        results.Add(new NullTenantTableAudit(table, count, sampleIds));
    }
}
