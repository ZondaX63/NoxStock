using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NoxStock.Settings;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace NoxStock.Documents;

public class DocumentNumberGenerator : IDocumentNumberGenerator, ITransientDependency
{
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly ISettingProvider _settingProvider;
    private readonly IDataFilter _dataFilter;

    public DocumentNumberGenerator(
        IRepository<Document, Guid> documentRepository,
        ISettingProvider settingProvider,
        IDataFilter dataFilter)
    {
        _documentRepository = documentRepository;
        _settingProvider = settingProvider;
        _dataFilter = dataFilter;
    }

    public async Task<string> GetNextAsync(DocumentType type)
    {
        var prefix = await GetPrefixAsync(type);
        var documentPrefix = $"{prefix}-{DateTime.UtcNow:yyyy}-";
        List<Document> documents;
        using (_dataFilter.Disable<ISoftDelete>())
        {
            documents = await _documentRepository.GetListAsync(x =>
                x.Type == type && x.DocNumber.StartsWith(documentPrefix));
        }

        var lastSequence = documents
            .Select(x => TryGetSequence(x.DocNumber, documentPrefix))
            .DefaultIfEmpty(0)
            .Max();

        return $"{documentPrefix}{lastSequence + 1:D6}";
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
