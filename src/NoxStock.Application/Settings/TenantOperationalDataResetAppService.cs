using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.Accounts;
using NoxStock.Brands;
using NoxStock.BusinessPartners;
using NoxStock.Categories;
using NoxStock.Documents;
using NoxStock.MultiTenancy;
using NoxStock.Permissions;
using NoxStock.Products;
using NoxStock.StockMovements;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.SettingManagement;

namespace NoxStock.Settings;

/// <summary>
/// Clears a tenant's operational data while deliberately preserving its identity,
/// license, company profile, preferences and host-managed exchange rates.
/// </summary>
[Authorize(NoxStockPermissions.Settings.Edit)]
public class TenantOperationalDataResetAppService : ApplicationService, ITenantOperationalDataResetAppService
{
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<ProductOEMCode, Guid> _productOemCodeRepository;
    private readonly IRepository<ProductCostHistory, Guid> _productCostHistoryRepository;
    private readonly IRepository<Brand, Guid> _brandRepository;
    private readonly IRepository<Category, Guid> _categoryRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<DocumentItem, Guid> _documentItemRepository;
    private readonly IRepository<DocumentExchangeRateSnapshot, Guid> _documentExchangeRateSnapshotRepository;
    private readonly IRepository<StockMovement, Guid> _stockMovementRepository;
    private readonly IRepository<AccountTransaction, Guid> _accountTransactionRepository;
    private readonly ISettingManager _settingManager;

    public TenantOperationalDataResetAppService(
        IRepository<Product, Guid> productRepository,
        IRepository<ProductOEMCode, Guid> productOemCodeRepository,
        IRepository<ProductCostHistory, Guid> productCostHistoryRepository,
        IRepository<Brand, Guid> brandRepository,
        IRepository<Category, Guid> categoryRepository,
        IRepository<BusinessPartner, Guid> businessPartnerRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<Document, Guid> documentRepository,
        IRepository<DocumentItem, Guid> documentItemRepository,
        IRepository<DocumentExchangeRateSnapshot, Guid> documentExchangeRateSnapshotRepository,
        IRepository<StockMovement, Guid> stockMovementRepository,
        IRepository<AccountTransaction, Guid> accountTransactionRepository,
        ISettingManager settingManager)
    {
        _productRepository = productRepository;
        _productOemCodeRepository = productOemCodeRepository;
        _productCostHistoryRepository = productCostHistoryRepository;
        _brandRepository = brandRepository;
        _categoryRepository = categoryRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _accountRepository = accountRepository;
        _documentRepository = documentRepository;
        _documentItemRepository = documentItemRepository;
        _documentExchangeRateSnapshotRepository = documentExchangeRateSnapshotRepository;
        _stockMovementRepository = stockMovementRepository;
        _accountTransactionRepository = accountTransactionRepository;
        _settingManager = settingManager;
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    public async Task<TenantOperationalDataResetResultDto> ResetAsync(TenantOperationalDataResetInputDto input)
    {
        CurrentTenant.RequireTenantId();
        if (!string.Equals(input.Confirmation?.Trim(), TenantOperationalDataResetInputDto.ConfirmationText, StringComparison.Ordinal))
        {
            throw new BusinessException(
                "NoxStock:TenantDataResetConfirmationRequired",
                $"Onay için {TenantOperationalDataResetInputDto.ConfirmationText} yazın.");
        }

        var result = new TenantOperationalDataResetResultDto
        {
            DocumentExchangeRateSnapshotCount = await DeleteAllAsync(_documentExchangeRateSnapshotRepository),
            ProductCostHistoryCount = await DeleteAllAsync(_productCostHistoryRepository),
            AccountTransactionCount = await DeleteAllAsync(_accountTransactionRepository),
            StockMovementCount = await DeleteAllAsync(_stockMovementRepository),
            DocumentItemCount = await DeleteAllAsync(_documentItemRepository),
            DocumentCount = await DeleteAllAsync(_documentRepository),
            ProductOemCodeCount = await DeleteAllAsync(_productOemCodeRepository),
            ProductCount = await DeleteAllAsync(_productRepository),
            AccountCount = await DeleteAllAsync(_accountRepository),
            BusinessPartnerCount = await DeleteAllAsync(_businessPartnerRepository),
            CategoryCount = await DeleteAllAsync(_categoryRepository),
            BrandCount = await DeleteAllAsync(_brandRepository)
        };

        await _settingManager.SetForCurrentTenantAsync(NoxStockSettings.DefaultCashAccountId, string.Empty);
        await _settingManager.SetForCurrentTenantAsync(NoxStockSettings.DefaultBankAccountId, string.Empty);

        return result;
    }

    private static async Task<int> DeleteAllAsync<TEntity>(IRepository<TEntity, Guid> repository)
        where TEntity : class, IEntity<Guid>
    {
        var entities = await repository.GetListAsync();
        if (entities.Count == 0)
        {
            return 0;
        }

        await repository.DeleteManyAsync(entities, autoSave: false);
        return entities.Count;
    }
}
