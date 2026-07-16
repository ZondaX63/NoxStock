using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NoxStock.Accounts;
using NoxStock.Settings;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;

namespace NoxStock.Tenants;

public class TenantDataSeederContributor : IDataSeedContributor, ITransientDependency
{
    private readonly ICurrentTenant _currentTenant;
    private readonly Volo.Abp.Domain.Repositories.IRepository<Account, System.Guid> _accountRepository;
    private readonly ISettingManager _settingManager;
    private readonly Volo.Abp.Guids.IGuidGenerator _guidGenerator;

    public TenantDataSeederContributor(
        ICurrentTenant currentTenant,
        Volo.Abp.Domain.Repositories.IRepository<Account, System.Guid> accountRepository,
        ISettingManager settingManager,
        Volo.Abp.Guids.IGuidGenerator guidGenerator)
    {
        _currentTenant = currentTenant;
        _accountRepository = accountRepository;
        _settingManager = settingManager;
        _guidGenerator = guidGenerator;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        // Only run for tenants, not for the host
        if (!_currentTenant.Id.HasValue)
        {
            return;
        }

        // Check if accounts already exist
        var count = await _accountRepository.GetCountAsync();
        if (count > 0)
        {
            return;
        }

        // 1. Create Default Cash Account
        var cashAccount = new Account(_guidGenerator.Create(), _currentTenant.Id, "Merkez Kasa", AccountType.Cash, "TRY");
        await _accountRepository.InsertAsync(cashAccount, autoSave: true);

        // 2. Create Default Bank Account
        var bankAccount = new Account(_guidGenerator.Create(), _currentTenant.Id, "Vadesiz TL Hesabı", AccountType.Bank, "TRY");
        await _accountRepository.InsertAsync(bankAccount, autoSave: true);

        // 3. Configure Default Company Settings
        await _settingManager.SetForCurrentTenantAsync(NoxStockSettings.DefaultCashAccountId, cashAccount.Id.ToString());
        await _settingManager.SetForCurrentTenantAsync(NoxStockSettings.DefaultBankAccountId, bankAccount.Id.ToString());
        await _settingManager.SetForCurrentTenantAsync(NoxStockSettings.DefaultCurrency, "TRY");
        await _settingManager.SetForCurrentTenantAsync(NoxStockSettings.DefaultUnit, "Adet");
    }
}
