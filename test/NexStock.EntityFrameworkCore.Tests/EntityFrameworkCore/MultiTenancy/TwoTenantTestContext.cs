using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using NexStock.MultiTenancy;

namespace NexStock.EntityFrameworkCore.MultiTenancy;

public sealed class TwoTenantTestContext
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IGuidGenerator _guidGenerator;

    public Tenant TenantA { get; private set; } = null!;
    public Tenant TenantB { get; private set; } = null!;
    public IdentityUser UserA { get; private set; } = null!;
    public IdentityUser UserB { get; private set; } = null!;

    public TwoTenantTestContext(
        ICurrentTenant currentTenant,
        ITenantManager tenantManager,
        ITenantRepository tenantRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IGuidGenerator guidGenerator)
    {
        _currentTenant = currentTenant;
        _tenantManager = tenantManager;
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _guidGenerator = guidGenerator;
    }

    public async Task InitializeAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        TenantA = await CreateTenantAsync($"tenant-a-{suffix}");
        TenantB = await CreateTenantAsync($"tenant-b-{suffix}");
        UserA = await CreateUserAsync(TenantA.Id, $"user-a-{suffix}", $"a-{suffix}@nexstock.test");
        UserB = await CreateUserAsync(TenantB.Id, $"user-b-{suffix}", $"b-{suffix}@nexstock.test");
    }

    public IDisposable ChangeToTenantA() => _currentTenant.Change(TenantA.Id, TenantA.Name);
    public IDisposable ChangeToTenantB() => _currentTenant.Change(TenantB.Id, TenantB.Name);
    public IDisposable ChangeToHost() => _currentTenant.Change(null);

    private async Task<Tenant> CreateTenantAsync(string name)
    {
        using (ChangeToHost())
        {
            var tenant = await _tenantManager.CreateAsync(name);
            tenant.SetNexStockActive(true);
            return await _tenantRepository.InsertAsync(tenant, autoSave: true);
        }
    }

    private async Task<IdentityUser> CreateUserAsync(Guid tenantId, string userName, string email)
    {
        using (_currentTenant.Change(tenantId))
        {
            var user = new IdentityUser(_guidGenerator.Create(), userName, email, tenantId);
            return await _userRepository.InsertAsync(user, autoSave: true);
        }
    }
}
