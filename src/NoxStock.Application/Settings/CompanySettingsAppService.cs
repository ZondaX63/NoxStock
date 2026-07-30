using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.Accounts;
using NoxStock.Permissions;
using NoxStock.MultiTenancy;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Volo.Abp.SettingManagement;

namespace NoxStock.Settings;

[Authorize(NoxStockPermissions.Settings.Default)]
public class CompanySettingsAppService : ApplicationService, ICompanySettingsAppService
{
    private readonly ISettingProvider _settingProvider;
    private readonly ISettingManager _settingManager;
    private readonly IRepository<Account, Guid> _accountRepository;

    public CompanySettingsAppService(
        ISettingProvider settingProvider,
        ISettingManager settingManager,
        IRepository<Account, Guid> accountRepository)
    {
        _settingProvider = settingProvider;
        _settingManager = settingManager;
        _accountRepository = accountRepository;
    }

    public async Task<CompanySettingsDto> GetAsync()
    {
        CurrentTenant.RequireTenantId();
        return new CompanySettingsDto
        {
            CompanyName = await GetValueAsync(NoxStockSettings.CompanyName),
            TaxOffice = await GetValueAsync(NoxStockSettings.TaxOffice),
            TaxNumber = await GetValueAsync(NoxStockSettings.TaxNumber),
            Address = await GetValueAsync(NoxStockSettings.Address),
            Phone = await GetValueAsync(NoxStockSettings.Phone),
            Email = await GetValueAsync(NoxStockSettings.Email),
            DefaultCurrency = await GetValueAsync(NoxStockSettings.DefaultCurrency),
            DefaultVatRate = await GetDecimalAsync(NoxStockSettings.DefaultVatRate, 20),
            DefaultUnit = await GetValueAsync(NoxStockSettings.DefaultUnit),
            DefaultCashAccountId = ParseGuid(await GetValueAsync(NoxStockSettings.DefaultCashAccountId)),
            DefaultBankAccountId = ParseGuid(await GetValueAsync(NoxStockSettings.DefaultBankAccountId)),
            SaleInvoiceNumberPrefix = await GetValueAsync(NoxStockSettings.SaleInvoiceNumberPrefix),
            PurchaseInvoiceNumberPrefix = await GetValueAsync(NoxStockSettings.PurchaseInvoiceNumberPrefix),
            OfferNumberPrefix = await GetValueAsync(NoxStockSettings.OfferNumberPrefix),
            SaleOrderNumberPrefix = await GetValueAsync(NoxStockSettings.SaleOrderNumberPrefix),
            PurchaseOrderNumberPrefix = await GetValueAsync(NoxStockSettings.PurchaseOrderNumberPrefix),
            PosSaleNumberPrefix = await GetValueAsync(NoxStockSettings.PosSaleNumberPrefix),
            DefaultLabelFormat = await GetValueAsync(NoxStockSettings.DefaultLabelFormat)
        };
    }

    [Authorize(NoxStockPermissions.Settings.Edit)]
    [NoxStock.Licensing.TenantLicenseWrite]
    public async Task<CompanySettingsDto> UpdateAsync(CompanySettingsDto input)
    {
        CurrentTenant.RequireTenantId();
        input.DefaultCurrency = NormalizeCurrency(input.DefaultCurrency);
        input.DefaultUnit = input.DefaultUnit.Trim();
        input.DefaultLabelFormat = input.DefaultLabelFormat.Trim().ToLowerInvariant();

        await ValidateAccountAsync(input.DefaultCashAccountId, AccountType.Cash);
        await ValidateAccountAsync(input.DefaultBankAccountId, AccountType.Bank, AccountType.CreditCard);

        await SetValueAsync(NoxStockSettings.CompanyName, input.CompanyName.Trim());
        await SetValueAsync(NoxStockSettings.TaxOffice, input.TaxOffice.Trim());
        await SetValueAsync(NoxStockSettings.TaxNumber, input.TaxNumber.Trim());
        await SetValueAsync(NoxStockSettings.Address, input.Address.Trim());
        await SetValueAsync(NoxStockSettings.Phone, input.Phone.Trim());
        await SetValueAsync(NoxStockSettings.Email, input.Email.Trim());
        await SetValueAsync(NoxStockSettings.DefaultCurrency, input.DefaultCurrency);
        await SetValueAsync(NoxStockSettings.DefaultVatRate, input.DefaultVatRate.ToString(CultureInfo.InvariantCulture));
        await SetValueAsync(NoxStockSettings.DefaultUnit, input.DefaultUnit);
        await SetValueAsync(NoxStockSettings.DefaultCashAccountId, input.DefaultCashAccountId?.ToString() ?? string.Empty);
        await SetValueAsync(NoxStockSettings.DefaultBankAccountId, input.DefaultBankAccountId?.ToString() ?? string.Empty);
        await SetValueAsync(NoxStockSettings.SaleInvoiceNumberPrefix, NormalizePrefix(input.SaleInvoiceNumberPrefix));
        await SetValueAsync(NoxStockSettings.PurchaseInvoiceNumberPrefix, NormalizePrefix(input.PurchaseInvoiceNumberPrefix));
        await SetValueAsync(NoxStockSettings.OfferNumberPrefix, NormalizePrefix(input.OfferNumberPrefix));
        await SetValueAsync(NoxStockSettings.SaleOrderNumberPrefix, NormalizePrefix(input.SaleOrderNumberPrefix));
        await SetValueAsync(NoxStockSettings.PurchaseOrderNumberPrefix, NormalizePrefix(input.PurchaseOrderNumberPrefix));
        await SetValueAsync(NoxStockSettings.PosSaleNumberPrefix, NormalizePrefix(input.PosSaleNumberPrefix));
        await SetValueAsync(NoxStockSettings.DefaultLabelFormat, input.DefaultLabelFormat);

        return await GetAsync();
    }

    private async Task<string> GetValueAsync(string name)
    {
        return await _settingProvider.GetOrNullAsync(name) ?? string.Empty;
    }

    private async Task<decimal> GetDecimalAsync(string name, decimal fallback)
    {
        var value = await GetValueAsync(name);
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : fallback;
    }

    private async Task SetValueAsync(string name, string value)
    {
        if (CurrentTenant.Id.HasValue)
        {
            await _settingManager.SetForCurrentTenantAsync(name, value);
            return;
        }

        await _settingManager.SetGlobalAsync(name, value);
    }

    private async Task ValidateAccountAsync(Guid? accountId, params AccountType[] supportedTypes)
    {
        if (!accountId.HasValue)
        {
            return;
        }

        var account = await _accountRepository.FindAsync(accountId.Value);
        if (account is null || !account.IsActive || Array.IndexOf(supportedTypes, account.Type) < 0)
        {
            throw new BusinessException("NoxStock:InvalidDefaultAccount");
        }
    }

    private static Guid? ParseGuid(string value)
    {
        return Guid.TryParse(value, out var result) ? result : null;
    }

    private static string NormalizeCurrency(string currency)
    {
        var value = currency.Trim().ToUpperInvariant();
        if (value is not ("TRY" or "USD" or "EUR"))
        {
            throw new BusinessException("NoxStock:UnsupportedCurrency");
        }

        return value;
    }

    private static string NormalizePrefix(string prefix)
    {
        return prefix.Trim().ToUpperInvariant();
    }
}
