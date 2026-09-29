using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Linq;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace NoxStock.TenantBackups;

public sealed class TenantBackupCleanupService : ITransientDependency
{
    public const int BatchSize = 50;
    private readonly IRepository<TenantBackup, Guid> _backups;
    private readonly IDataFilter<IMultiTenant> _dataFilter;
    private readonly ITenantBackupStorage _storage;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _uow;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly ILogger<TenantBackupCleanupService> _logger;

    public TenantBackupCleanupService(IRepository<TenantBackup, Guid> backups, IDataFilter<IMultiTenant> dataFilter,
        ITenantBackupStorage storage, ICurrentTenant currentTenant, IUnitOfWorkManager uow, IAsyncQueryableExecuter asyncExecuter,
        ILogger<TenantBackupCleanupService> logger)
    { _backups = backups; _dataFilter = dataFilter; _storage = storage; _currentTenant = currentTenant; _uow = uow; _asyncExecuter = asyncExecuter; _logger = logger; }

    public async Task<int> DeleteExpiredAsync(DateTime utcNow)
    {
        using (_dataFilter.Disable())
        {
            var query = await _backups.GetQueryableAsync();
            var candidates = await _asyncExecuter.ToListAsync(query
                .Where(x => x.ExpiresAtUtc <= utcNow && (x.Status == TenantBackupStatus.Succeeded || x.Status == TenantBackupStatus.Failed))
                .OrderBy(x => x.ExpiresAtUtc).Take(BatchSize));
            var deleted = 0;
            foreach (var backup in candidates)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(backup.StorageName))
                    {
                        using (_currentTenant.Change(backup.TenantId))
                            await _storage.DeleteAsync(backup.StorageName);
                    }
                    using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
                    await _backups.DeleteAsync(backup, autoSave: true);
                    await uow.CompleteAsync();
                    deleted++;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Tenant backup retention cleanup failed");
                }
            }
            return deleted;
        }
    }
}

public sealed class TenantBackupCleanupWorker : AsyncPeriodicBackgroundWorkerBase
{
    public TenantBackupCleanupWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = (int)TimeSpan.FromHours(1).TotalMilliseconds;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var cleanup = workerContext.ServiceProvider.GetRequiredService<TenantBackupCleanupService>();
        await cleanup.DeleteExpiredAsync(DateTime.UtcNow);
    }
}
