using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NoxStock.Settings;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;
using Volo.Abp.MultiTenancy;
using NoxStock.MultiTenancy;

namespace NoxStock.Documents;

public class DocumentNumberGenerator : IDocumentNumberGenerator, ITransientDependency
{
    private readonly ISettingProvider _settingProvider;
    private readonly IRepository<DocumentNumberSequence, Guid> _sequenceRepository;
    private readonly IDocumentNumberSequenceStore _sequenceStore;
    private readonly ICurrentTenant _currentTenant;

    public DocumentNumberGenerator(
        ISettingProvider settingProvider,
        IRepository<DocumentNumberSequence, Guid> sequenceRepository,
        IDocumentNumberSequenceStore sequenceStore,
        ICurrentTenant currentTenant)
    {
        _settingProvider = settingProvider;
        _sequenceRepository = sequenceRepository;
        _sequenceStore = sequenceStore;
        _currentTenant = currentTenant;
    }

    public async Task<string> GetNextAsync(DocumentType type)
    {
        var prefix = await GetPrefixAsync(type);
        var year = DateTime.UtcNow.Year;
        var tenantId = _currentTenant.RequireTenantId();
        var sequence = await _sequenceStore.TryAllocateAsync(tenantId, type, year, prefix);
        if (!sequence.HasValue)
        {
            var row = await _sequenceRepository.FirstOrDefaultAsync(x => x.DocumentType == type && x.Year == year && x.Prefix == prefix);
            if (row is null) { row = new DocumentNumberSequence(Guid.NewGuid(), tenantId, type, year, prefix, 1); await _sequenceRepository.InsertAsync(row, autoSave: true); }
            else { row.LastNumber++; await _sequenceRepository.UpdateAsync(row, autoSave: true); }
            sequence = row.LastNumber;
        }
        return $"{prefix}-{year}-{sequence.Value:D6}";
    }

    private async Task<string> GetPrefixAsync(DocumentType type)
    {
        var settingName = type switch
        {
            DocumentType.SaleOrder => NoxStockSettings.SaleOrderNumberPrefix,
            DocumentType.PurchaseOrder => NoxStockSettings.PurchaseOrderNumberPrefix,
            DocumentType.SaleInvoice => NoxStockSettings.SaleInvoiceNumberPrefix,
            DocumentType.PurchaseInvoice => NoxStockSettings.PurchaseInvoiceNumberPrefix,
            DocumentType.Offer => NoxStockSettings.OfferNumberPrefix,
            _ => string.Empty
        };

        return string.IsNullOrEmpty(settingName)
            ? "BEL"
            : (await _settingProvider.GetOrNullAsync(settingName) ?? "BEL").Trim().ToUpperInvariant();
    }

    private static int TryGetSequence(string number, string documentPrefix)
    {
        var sequence = number[documentPrefix.Length..];
        return int.TryParse(sequence, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }
}
