using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MiniExcelLibs;
using MiniExcelLibs.Attributes;
using Microsoft.AspNetCore.Authorization;
using NoxStock.Licensing;
using NoxStock.MultiTenancy;
using NoxStock.Permissions;
using NoxStock.Products;
using NoxStock.StockMovements;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace NoxStock.InventoryCounts;

[Authorize(NoxStockPermissions.InventoryCounts.Default)]
public class InventoryCountAppService : NoxStockAppService, IInventoryCountAppService
{
    private readonly IRepository<InventoryCount, Guid> _countRepository;
    private readonly IRepository<InventoryCountItem, Guid> _itemRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<StockMovement, Guid> _movementRepository;
    private readonly ITenantLicenseAccessChecker _licenseAccessChecker;

    public InventoryCountAppService(
        IRepository<InventoryCount, Guid> countRepository,
        IRepository<InventoryCountItem, Guid> itemRepository,
        IRepository<Product, Guid> productRepository,
        IRepository<StockMovement, Guid> movementRepository,
        ITenantLicenseAccessChecker licenseAccessChecker)
    {
        _countRepository = countRepository;
        _itemRepository = itemRepository;
        _productRepository = productRepository;
        _movementRepository = movementRepository;
        _licenseAccessChecker = licenseAccessChecker;
    }

    [Authorize(NoxStockPermissions.InventoryCounts.Create)]
    [TenantLicenseWrite]
    public async Task<InventoryCountDto> GetOrCreateDraftAsync()
    {
        var tenantId = CurrentTenant.RequireTenantId();
        var query = await _countRepository.WithDetailsAsync(x => x.Items);
        var activeCount = await AsyncExecuter.FirstOrDefaultAsync(query
            .Where(x => x.Status == InventoryCountStatus.Draft || x.Status == InventoryCountStatus.AwaitingApproval)
            .OrderByDescending(x => x.CreationTime));

        if (activeCount is not null)
        {
            return await MapAsync(activeCount);
        }

        var now = Clock.Now;
        var reference = await CreateReferenceNumberAsync(now);
        var entity = new InventoryCount(
            GuidGenerator.Create(),
            tenantId,
            reference,
            $"{now:yyyy-MM-dd} Genel Sayımı",
            now,
            CurrentUser.Name ?? CurrentUser.UserName ?? "-");

        await _countRepository.InsertAsync(entity, autoSave: true);
        return await MapAsync(entity);
    }

    public async Task<InventoryCountDto> GetAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        var query = await _countRepository.WithDetailsAsync(x => x.Items);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id))
                     ?? throw new BusinessException("NoxStock:InventoryCountNotFound");
        return await MapAsync(entity);
    }

    public async Task<PagedResultDto<InventoryCountListDto>> GetListAsync(InventoryCountGetListInput input)
    {
        CurrentTenant.RequireTenantId();
        input.SkipCount = Math.Clamp(input.SkipCount, 0, 10_000);
        input.MaxResultCount = Math.Clamp(input.MaxResultCount, 1, 100);
        var query = (await _countRepository.GetQueryableAsync())
            .WhereIf(input.Status.HasValue, x => x.Status == input.Status)
            .OrderByDescending(x => x.CountDate)
            .ThenByDescending(x => x.CreationTime);
        var total = await AsyncExecuter.CountAsync(query);
        var rows = await AsyncExecuter.ToListAsync(query
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount));

        return new PagedResultDto<InventoryCountListDto>(total, rows.Select(MapList).ToList());
    }

    [Authorize(NoxStockPermissions.InventoryCounts.Create)]
    [TenantLicenseWrite]
    [UnitOfWork(true)]
    public async Task<InventoryCountDto> SaveDraftAsync(SaveInventoryCountDto input)
    {
        var tenantId = CurrentTenant.RequireTenantId();
        var name = input.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessException("NoxStock:InventoryCountNameRequired");
        }

        var duplicateProductId = input.Items
            .GroupBy(x => x.ProductId)
            .FirstOrDefault(x => x.Count() > 1)?.Key;
        if (duplicateProductId.HasValue)
        {
            throw new BusinessException("NoxStock:InventoryCountDuplicateProduct")
                .WithData("ProductId", duplicateProductId.Value);
        }

        if (input.Items.Any(x => x.CountedQuantity < 0m))
        {
            throw new BusinessException("NoxStock:InventoryCountNegativeQuantity");
        }

        InventoryCount entity;
        if (input.Id.HasValue)
        {
            var query = await _countRepository.WithDetailsAsync(x => x.Items);
            entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == input.Id.Value))
                     ?? throw new BusinessException("NoxStock:InventoryCountNotFound");
            EnsureDraft(entity);
        }
        else
        {
            var now = Clock.Now;
            entity = new InventoryCount(
                GuidGenerator.Create(), tenantId, await CreateReferenceNumberAsync(now), name, now,
                CurrentUser.Name ?? CurrentUser.UserName ?? "-");
            await _countRepository.InsertAsync(entity, autoSave: false);
        }

        entity.Name = name;
        var productIds = input.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = productIds.Count == 0
            ? new List<Product>()
            : await _productRepository.GetListAsync(x => productIds.Contains(x.Id));
        if (products.Count != productIds.Count)
        {
            throw new BusinessException("NoxStock:InventoryCountProductNotFound");
        }
        if (products.Any(x => !x.TrackStock))
        {
            throw new BusinessException("NoxStock:InventoryCountUntrackedProduct");
        }

        if (entity.Items.Count > 0)
        {
            await _itemRepository.DeleteManyAsync(entity.Items.ToList(), autoSave: false);
            entity.Items.Clear();
        }

        var productById = products.ToDictionary(x => x.Id);
        foreach (var source in input.Items)
        {
            var product = productById[source.ProductId];
            var item = new InventoryCountItem(GuidGenerator.Create(), tenantId, entity.Id, product.Id)
            {
                ProductName = product.Name,
                Sku = product.Sku,
                Barcode = product.Barcode,
                Unit = string.IsNullOrWhiteSpace(product.Unit) ? "Adet" : product.Unit,
                SystemQuantity = RoundQuantity(product.Quantity),
                CountedQuantity = RoundQuantity(source.CountedQuantity),
                Note = source.Note?.Trim(),
                IsQuickCreatedProduct = product.IsIncompleteProfile
            };
            entity.Items.Add(item);
            await _itemRepository.InsertAsync(item, autoSave: false);
        }

        entity.CountedProductCount = input.Items.Count;
        entity.TotalCountedQuantity = RoundQuantity(input.Items.Sum(x => x.CountedQuantity));
        await _countRepository.UpdateAsync(entity, autoSave: true);
        return await GetAsync(entity.Id);
    }

    [Authorize(NoxStockPermissions.InventoryCounts.Create)]
    [TenantLicenseWrite]
    [UnitOfWork(true)]
    public async Task<InventoryCountDto> RestoreBackupAsync(RestoreInventoryCountBackupDto input)
    {
        CurrentTenant.RequireTenantId();
        if (input.Items is null)
        {
            throw new BusinessException("NoxStock:InventoryCountInvalidBackup");
        }

        var products = await _productRepository.GetListAsync(x => x.TrackStock);
        var byId = products.ToDictionary(x => x.Id);
        var bySku = products
            .Where(x => !string.IsNullOrWhiteSpace(x.Sku))
            .GroupBy(x => x.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var byBarcode = products
            .Where(x => !string.IsNullOrWhiteSpace(x.Barcode))
            .GroupBy(x => x.Barcode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var items = new List<SaveInventoryCountItemDto>();
        foreach (var source in input.Items)
        {
            Product? product = null;
            if (source.ProductId.HasValue)
            {
                byId.TryGetValue(source.ProductId.Value, out product);
            }
            if (product is null && !string.IsNullOrWhiteSpace(source.Sku))
            {
                bySku.TryGetValue(source.Sku.Trim(), out product);
            }
            if (product is null && !string.IsNullOrWhiteSpace(source.Barcode))
            {
                byBarcode.TryGetValue(source.Barcode.Trim(), out product);
            }
            if (product is null || source.CountedQuantity < 0m)
            {
                throw new BusinessException("NoxStock:InventoryCountBackupProductNotFound")
                    .WithData("Sku", source.Sku ?? string.Empty)
                    .WithData("Barcode", source.Barcode ?? string.Empty);
            }
            if (items.Any(x => x.ProductId == product.Id))
            {
                throw new BusinessException("NoxStock:InventoryCountDuplicateProduct")
                    .WithData("ProductId", product.Id);
            }
            items.Add(new SaveInventoryCountItemDto
            {
                ProductId = product.Id,
                CountedQuantity = RoundQuantity(source.CountedQuantity),
                Note = source.Note?.Trim()
            });
        }

        return await SaveDraftAsync(new SaveInventoryCountDto
        {
            Id = input.Id,
            Name = input.Name,
            Items = items
        });
    }

    [Authorize(NoxStockPermissions.InventoryCounts.Create)]
    [TenantLicenseWrite]
    public async Task<InventoryCountDto> SubmitForApprovalAsync(Guid id)
    {
        var entity = await GetEntityWithItemsAsync(id);
        EnsureDraft(entity);
        if (entity.Items.Count == 0)
        {
            throw new BusinessException("NoxStock:InventoryCountEmpty");
        }
        entity.Status = InventoryCountStatus.AwaitingApproval;
        entity.SubmittedAt = Clock.Now;
        await _countRepository.UpdateAsync(entity, autoSave: true);
        return await MapAsync(entity);
    }

    [Authorize(NoxStockPermissions.InventoryCounts.Create)]
    [TenantLicenseWrite]
    public async Task<InventoryCountDto> ReopenDraftAsync(Guid id)
    {
        var entity = await GetEntityWithItemsAsync(id);
        if (entity.Status != InventoryCountStatus.AwaitingApproval)
        {
            throw new BusinessException("NoxStock:InventoryCountCannotReopen");
        }
        entity.Status = InventoryCountStatus.Draft;
        entity.SubmittedAt = null;
        await _countRepository.UpdateAsync(entity, autoSave: true);
        return await MapAsync(entity);
    }

    [Authorize(NoxStockPermissions.InventoryCounts.Complete)]
    [TenantLicenseWrite]
    [UnitOfWork(true)]
    public async Task<InventoryCountDto> CompleteAsync(CompleteInventoryCountDto input)
    {
        CurrentTenant.RequireTenantId();
        if (!input.ConfirmReset)
        {
            throw new BusinessException("NoxStock:InventoryCountConfirmationRequired");
        }

        var entity = await GetEntityWithItemsAsync(input.Id);
        if (entity.Status == InventoryCountStatus.Completed)
        {
            return await MapAsync(entity);
        }
        if (entity.Items.Count == 0)
        {
            throw new BusinessException("NoxStock:InventoryCountEmpty");
        }

        var products = await _productRepository.GetListAsync(x => x.TrackStock);
        var productById = products.ToDictionary(x => x.Id);
        var itemByProductId = entity.Items.ToDictionary(x => x.ProductId);
        if (itemByProductId.Keys.Any(id => !productById.ContainsKey(id)))
        {
            throw new BusinessException("NoxStock:InventoryCountProductNotFound");
        }

        var now = Clock.Now;
        var movements = new List<StockMovement>();
        var changedProducts = new List<Product>();
        var zeroed = 0;
        var adjusted = 0;

        foreach (var product in products)
        {
            var targetQuantity = itemByProductId.TryGetValue(product.Id, out var item)
                ? RoundQuantity(item.CountedQuantity)
                : 0m;
            if (product.Quantity == targetQuantity)
            {
                continue;
            }

            var delta = targetQuantity - product.Quantity;
            if (targetQuantity == 0m && !itemByProductId.ContainsKey(product.Id))
            {
                zeroed++;
            }
            else
            {
                adjusted++;
            }

            product.Quantity = targetQuantity;
            changedProducts.Add(product);
            movements.Add(new StockMovement(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                product.Id,
                StockMovementType.InventoryCount,
                delta,
                targetQuantity,
                now,
                description: L["InventoryCount:MovementDescription", entity.ReferenceNumber].Value));
        }

        if (changedProducts.Count > 0)
        {
            await _productRepository.UpdateManyAsync(changedProducts, autoSave: false);
            await _movementRepository.InsertManyAsync(movements, autoSave: false);
        }

        entity.Status = InventoryCountStatus.Completed;
        entity.CompletedAt = now;
        entity.CompletedByUserId = CurrentUser.Id;
        entity.CompletedByName = CurrentUser.Name ?? CurrentUser.UserName ?? "-";
        entity.CountedProductCount = entity.Items.Count;
        entity.TotalCountedQuantity = RoundQuantity(entity.Items.Sum(x => x.CountedQuantity));
        entity.ZeroedProductCount = zeroed;
        entity.AdjustedProductCount = adjusted;
        await _countRepository.UpdateAsync(entity, autoSave: true);
        return await MapAsync(entity);
    }

    [Authorize(NoxStockPermissions.InventoryCounts.Create)]
    [Authorize(NoxStockPermissions.Products.Create)]
    [TenantLicenseWrite]
    public async Task<QuickCreateInventoryProductResultDto> QuickCreateProductAsync(QuickCreateInventoryProductDto input)
    {
        var tenantId = CurrentTenant.RequireTenantId();
        var name = input.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessException("NoxStock:ProductNameRequired");
        }
        if (input.CountedQuantity < 0m)
        {
            throw new BusinessException("NoxStock:InventoryCountNegativeQuantity");
        }

        var barcode = input.Barcode?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(barcode) &&
            await _productRepository.AnyAsync(x => x.Barcode == barcode))
        {
            throw new BusinessException("NoxStock:InventoryCountBarcodeExists")
                .WithData("Barcode", barcode);
        }

        await _licenseAccessChecker.EnsureLimitAsync(
            LicenseLimitNames.MaxProducts,
            await _productRepository.GetCountAsync());

        var product = new Product(
            GuidGenerator.Create(), tenantId, name,
            $"SYM-{Clock.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..5].ToUpperInvariant()}")
        {
            Barcode = barcode,
            Description = input.Note?.Trim() ?? string.Empty,
            Unit = "Adet",
            ShelfLocation = string.Empty,
            SalePrice = RoundQuantity(input.SalePrice ?? 0m),
            PurchaseCurrency = "EUR",
            ExchangeRateAtPurchase = 1m,
            VatRate = 20,
            ProfitMarkupPercent = 30m,
            PricingMode = ProductPricingMode.Manual,
            Quantity = 0m,
            TrackStock = true,
            IsIncompleteProfile = true
        };
        await _productRepository.InsertAsync(product, autoSave: true);

        return new QuickCreateInventoryProductResultDto
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Sku = product.Sku,
            Barcode = product.Barcode,
            Unit = product.Unit,
            SystemQuantity = 0m,
            CountedQuantity = RoundQuantity(input.CountedQuantity),
            Note = input.Note?.Trim(),
            IsQuickCreatedProduct = true
        };
    }

    [Authorize(NoxStockPermissions.InventoryCounts.Create)]
    [TenantLicenseWrite]
    public async Task<InventoryCountImportResultDto> ImportAsync(InventoryCountImportInputDto input)
    {
        CurrentTenant.RequireTenantId();
        ValidateImportFile(input);
        List<InventoryCountSpreadsheetRow> rows;
        try
        {
            using var stream = new MemoryStream(input.Content, writable: false);
            rows = (await MiniExcel.QueryAsync<InventoryCountSpreadsheetRow>(
                stream,
                excelType: string.Equals(Path.GetExtension(input.FileName), ".csv", StringComparison.OrdinalIgnoreCase)
                    ? ExcelType.CSV
                    : ExcelType.XLSX)).ToList();
        }
        catch (Exception exception) when (exception is not BusinessException)
        {
            throw new BusinessException("NoxStock:InventoryCountInvalidWorkbook");
        }

        var products = await _productRepository.GetListAsync(x => x.TrackStock);
        var bySku = products.Where(x => !string.IsNullOrWhiteSpace(x.Sku))
            .GroupBy(x => x.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var byBarcode = products.Where(x => !string.IsNullOrWhiteSpace(x.Barcode))
            .GroupBy(x => x.Barcode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var quantities = new Dictionary<Guid, decimal>();
        var result = new InventoryCountImportResultDto();

        for (var index = 0; index < rows.Count; index++)
        {
            var source = rows[index];
            var rowNumber = index + 2;
            var sku = CellText(source.Sku);
            var barcode = CellText(source.Barcode);
            var quantity = ParseDecimal(source.Quantity);
            if (string.IsNullOrWhiteSpace(sku) && string.IsNullOrWhiteSpace(barcode) && quantity is null)
            {
                continue;
            }
            result.SourceRowCount++;

            Product? product = null;
            if (!string.IsNullOrWhiteSpace(sku)) bySku.TryGetValue(sku, out product);
            if (product is null && !string.IsNullOrWhiteSpace(barcode)) byBarcode.TryGetValue(barcode, out product);
            if (product is null)
            {
                AddIssue(result, rowNumber, L["InventoryCount:ImportProductNotFound"].Value);
                result.SkippedRowCount++;
                continue;
            }
            if (quantity is null || quantity < 0m)
            {
                AddIssue(result, rowNumber, L["InventoryCount:ImportQuantityInvalid"].Value);
                result.SkippedRowCount++;
                continue;
            }

            quantities[product.Id] = RoundQuantity(
                quantities.GetValueOrDefault(product.Id) + quantity.Value);
        }

        var productById = products.ToDictionary(x => x.Id);
        result.Items = quantities.Select(pair =>
        {
            var product = productById[pair.Key];
            return new InventoryCountImportedItemDto
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Sku = product.Sku,
                Barcode = product.Barcode,
                Unit = product.Unit,
                SystemQuantity = RoundQuantity(product.Quantity),
                CountedQuantity = RoundQuantity(pair.Value)
            };
        }).OrderBy(x => x.ProductName).ToList();
        result.MatchedProductCount = result.Items.Count;
        return result;
    }

    private async Task<InventoryCount> GetEntityWithItemsAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        var query = await _countRepository.WithDetailsAsync(x => x.Items);
        return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id))
               ?? throw new BusinessException("NoxStock:InventoryCountNotFound");
    }

    private static void EnsureDraft(InventoryCount entity)
    {
        if (entity.Status != InventoryCountStatus.Draft)
        {
            throw new BusinessException("NoxStock:InventoryCountNotEditable");
        }
    }

    private async Task<InventoryCountDto> MapAsync(InventoryCount entity)
    {
        var countedProductIds = entity.Items.Select(x => x.ProductId).ToList();
        var zeroQuery = (await _productRepository.GetQueryableAsync())
            .Where(x => x.TrackStock && x.Quantity != 0m);
        if (countedProductIds.Count > 0)
        {
            zeroQuery = zeroQuery.Where(x => !countedProductIds.Contains(x.Id));
        }

        return new InventoryCountDto
        {
            Id = entity.Id,
            CreationTime = entity.CreationTime,
            LastModificationTime = entity.LastModificationTime,
            ReferenceNumber = entity.ReferenceNumber,
            Name = entity.Name,
            CountDate = entity.CountDate,
            Status = entity.Status,
            CountedByName = entity.CountedByName,
            SubmittedAt = entity.SubmittedAt,
            CompletedAt = entity.CompletedAt,
            CompletedByName = entity.CompletedByName,
            CountedProductCount = entity.CountedProductCount,
            TotalCountedQuantity = entity.TotalCountedQuantity,
            ZeroedProductCount = entity.ZeroedProductCount,
            AdjustedProductCount = entity.AdjustedProductCount,
            ProductsThatWillBeZeroed = entity.Status == InventoryCountStatus.Completed
                ? entity.ZeroedProductCount
                : await AsyncExecuter.CountAsync(zeroQuery),
            Items = entity.Items.OrderBy(x => x.ProductName).Select(x => new InventoryCountItemDto
            {
                Id = x.Id,
                ProductId = x.ProductId,
                ProductName = x.ProductName,
                Sku = x.Sku,
                Barcode = x.Barcode,
                Unit = x.Unit,
                SystemQuantity = x.SystemQuantity,
                CountedQuantity = x.CountedQuantity,
                Note = x.Note,
                IsQuickCreatedProduct = x.IsQuickCreatedProduct
            }).ToList()
        };
    }

    private static InventoryCountListDto MapList(InventoryCount entity)
    {
        return new InventoryCountListDto
        {
            Id = entity.Id,
            ReferenceNumber = entity.ReferenceNumber,
            Name = entity.Name,
            CountDate = entity.CountDate,
            Status = entity.Status,
            CountedByName = entity.CountedByName,
            CompletedAt = entity.CompletedAt,
            CountedProductCount = entity.CountedProductCount,
            TotalCountedQuantity = entity.TotalCountedQuantity,
            ZeroedProductCount = entity.ZeroedProductCount,
            AdjustedProductCount = entity.AdjustedProductCount
        };
    }

    private async Task<string> CreateReferenceNumberAsync(DateTime now)
    {
        var prefix = $"SYM-{now:yyyyMMdd}";
        var count = await _countRepository.CountAsync(x => x.ReferenceNumber.StartsWith(prefix));
        return count == 0 ? prefix : $"{prefix}-{count + 1:00}";
    }

    private static void ValidateImportFile(InventoryCountImportInputDto input)
    {
        var extension = Path.GetExtension(input.FileName);
        if (input.Content.Length == 0 || input.Content.Length > InventoryCountImportInputDto.MaximumFileSize)
        {
            throw new BusinessException("NoxStock:InventoryCountInvalidFileSize");
        }
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("NoxStock:InventoryCountInvalidFileType");
        }
    }

    private static string CellText(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text.Trim(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
            _ => value.ToString()?.Trim() ?? string.Empty
        };
    }

    private static decimal? ParseDecimal(object? value)
    {
        if (value is null) return null;
        if (value is decimal decimalValue) return decimalValue;
        if (value is IConvertible convertible && value is not string)
        {
            try { return Convert.ToDecimal(convertible, CultureInfo.InvariantCulture); }
            catch { return null; }
        }
        var text = CellText(value).Replace(" ", string.Empty).Replace("\u00A0", string.Empty);
        if (string.IsNullOrWhiteSpace(text)) return null;
        var comma = text.LastIndexOf(',');
        var dot = text.LastIndexOf('.');
        if (comma >= 0 && dot >= 0)
        {
            var decimalSeparator = comma > dot ? ',' : '.';
            var groupSeparator = decimalSeparator == ',' ? "." : ",";
            text = text.Replace(groupSeparator, string.Empty).Replace(decimalSeparator, '.');
            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var mixed) ? mixed : null;
        }
        if (comma >= 0)
        {
            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("tr-TR"), out var tr) ? tr : null;
        }
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariant) ? invariant : null;
    }

    private static decimal RoundQuantity(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static void AddIssue(InventoryCountImportResultDto result, int rowNumber, string message)
    {
        if (result.Issues.Count < 50)
        {
            result.Issues.Add(new ProductImportIssueDto { RowNumber = rowNumber, Message = message });
        }
    }

    private sealed class InventoryCountSpreadsheetRow
    {
        [ExcelColumnName("SKU")] public object? Sku { get; set; }
        [ExcelColumnName("Barkod")] public object? Barcode { get; set; }
        [ExcelColumnName("Miktar")] public object? Quantity { get; set; }
    }
}
