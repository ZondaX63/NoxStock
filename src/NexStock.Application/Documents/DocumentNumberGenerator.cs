using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NexStock.Settings;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace NexStock.Documents;

public class DocumentNumberGenerator : IDocumentNumberGenerator, ITransientDependency
{
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly ISettingProvider _settingProvider;

    public DocumentNumberGenerator(
        IRepository<Document, Guid> documentRepository,
        ISettingProvider settingProvider)
    {
        _documentRepository = documentRepository;
        _settingProvider = settingProvider;
    }

    public async Task<string> GetNextAsync(DocumentType type)
    {
        var prefix = await GetPrefixAsync(type);
        var documentPrefix = $"{prefix}-{DateTime.UtcNow:yyyy}-";
        var documents = await _documentRepository.GetListAsync(x =>
            x.Type == type && x.DocNumber.StartsWith(documentPrefix));

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
            DocumentType.SaleOrder => NexStockSettings.SaleOrderNumberPrefix,
            DocumentType.PurchaseOrder => NexStockSettings.PurchaseOrderNumberPrefix,
            DocumentType.SaleInvoice => NexStockSettings.SaleInvoiceNumberPrefix,
            DocumentType.PurchaseInvoice => NexStockSettings.PurchaseInvoiceNumberPrefix,
            DocumentType.Offer => NexStockSettings.OfferNumberPrefix,
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
