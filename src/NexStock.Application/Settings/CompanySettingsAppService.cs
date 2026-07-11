using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NexStock.Accounts;
using NexStock.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Volo.Abp.SettingManagement;

namespace NexStock.Settings;

[Authorize(NexStockPermissions.Settings.Default)]
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
        return new CompanySettingsDto
        {
            CompanyName = await GetValueAsync(NexStockSettings.CompanyName),
            TaxOffice = await GetValueAsync(NexStockSettings.TaxOffice),
            TaxNumber = await GetValueAsync(NexStockSettings.TaxNumber),
            Address = await GetValueAsync(NexStockSettings.Address),
            Phone = await GetValueAsync(NexStockSettings.Phone),
            Email = await GetValueAsync(NexStockSettings.Email),
            DefaultCurrency = await GetValueAsync(NexStockSettings.DefaultCurrency),
            DefaultVatRate = await GetDecimalAsync(NexStockSettings.DefaultVatRate, 20),
            DefaultUnit = await GetValueAsync(NexStockSettings.DefaultUnit),
            DefaultCashAccountId = ParseGuid(await GetValueAsync(NexStockSettings.DefaultCashAccountId)),
            DefaultBankAccountId = ParseGuid(await GetValueAsync(NexStockSettings.DefaultBankAccountId)),
            SaleInvoiceNumberPrefix = await GetValueAsync(NexStockSettings.SaleInvoiceNumberPrefix),
            PurchaseInvoiceNumberPrefix = await GetValueAsync(NexStockSettings.PurchaseInvoiceNumberPrefix),
            OfferNumberPrefix = await GetValueAsync(NexStockSettings.OfferNumberPrefix),
            SaleOrderNumberPrefix = await GetValueAsync(NexStockSettings.SaleOrderNumberPrefix),
            PurchaseOrderNumberPrefix = await GetValueAsync(NexStockSettings.PurchaseOrderNumberPrefix),
            PosSaleNumberPrefix = await GetValueAsync(NexStockSettings.PosSaleNumberPrefix),
            DefaultLabelFormat = await GetValueAsync(NexStockSettings.DefaultLabelFormat)
        };
    }

    [Authorize(NexStockPermissions.Settings.Edit)]
    public async Task<CompanySettingsDto> UpdateAsync(CompanySettingsDto input)
    {
        input.DefaultCurrency = NormalizeCurrency(input.DefaultCurrency);
        input.DefaultUnit = input.DefaultUnit.Trim();
        input.DefaultLabelFormat = input.DefaultLabelFormat.Trim().ToLowerInvariant();

        await ValidateAccountAsync(input.DefaultCashAccountId, AccountType.Cash);
        await ValidateAccountAsync(input.DefaultBankAccountId, AccountType.Bank, AccountType.CreditCard);

        await SetValueAsync(NexStockSettings.CompanyName, input.CompanyName.Trim());
        await SetValueAsync(NexStockSettings.TaxOffice, input.TaxOffice.Trim());
        await SetValueAsync(NexStockSettings.TaxNumber, input.TaxNumber.Trim());
        await SetValueAsync(NexStockSettings.Address, input.Address.Trim());
        await SetValueAsync(NexStockSettings.Phone, input.Phone.Trim());
        await SetValueAsync(NexStockSettings.Email, input.Email.Trim());
        await SetValueAsync(NexStockSettings.DefaultCurrency, input.DefaultCurrency);
        await SetValueAsync(NexStockSettings.DefaultVatRate, input.DefaultVatRate.ToString(CultureInfo.InvariantCulture));
        await SetValueAsync(NexStockSettings.DefaultUnit, input.DefaultUnit);
        await SetValueAsync(NexStockSettings.DefaultCashAccountId, input.DefaultCashAccountId?.ToString() ?? string.Empty);
        await SetValueAsync(NexStockSettings.DefaultBankAccountId, input.DefaultBankAccountId?.ToString() ?? string.Empty);
        await SetValueAsync(NexStockSettings.SaleInvoiceNumberPrefix, NormalizePrefix(input.SaleInvoiceNumberPrefix));
        await SetValueAsync(NexStockSettings.PurchaseInvoiceNumberPrefix, NormalizePrefix(input.PurchaseInvoiceNumberPrefix));
        await SetValueAsync(NexStockSettings.OfferNumberPrefix, NormalizePrefix(input.OfferNumberPrefix));
        await SetValueAsync(NexStockSettings.SaleOrderNumberPrefix, NormalizePrefix(input.SaleOrderNumberPrefix));
        await SetValueAsync(NexStockSettings.PurchaseOrderNumberPrefix, NormalizePrefix(input.PurchaseOrderNumberPrefix));
        await SetValueAsync(NexStockSettings.PosSaleNumberPrefix, NormalizePrefix(input.PosSaleNumberPrefix));
        await SetValueAsync(NexStockSettings.DefaultLabelFormat, input.DefaultLabelFormat);

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
            throw new BusinessException("NexStock:InvalidDefaultAccount");
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
            throw new BusinessException("NexStock:UnsupportedCurrency");
        }

        return value;
    }

    private static string NormalizePrefix(string prefix)
    {
        return prefix.Trim().ToUpperInvariant();
    }
}
