using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.BusinessPartners;
using NoxStock.Documents;
using NoxStock.Permissions;
using NoxStock.Products;
using NoxStock.MultiTenancy;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace NoxStock.Notifications;

[Authorize(NoxStockPermissions.Notifications.Default)]
public class OperationalAlertsAppService : ApplicationService, IOperationalAlertsAppService
{
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;

    public OperationalAlertsAppService(
        IRepository<Product, Guid> productRepository,
        IRepository<Document, Guid> documentRepository,
        IRepository<BusinessPartner, Guid> businessPartnerRepository)
    {
        _productRepository = productRepository;
        _documentRepository = documentRepository;
        _businessPartnerRepository = businessPartnerRepository;
    }

    public async Task<OperationalAlertsSummaryDto> GetSummaryAsync()
    {
        CurrentTenant.RequireTenantId();
        var today = Clock.Now.Date;
        var products = await _productRepository.GetQueryableAsync();
        var documents = await _documentRepository.GetQueryableAsync();
        var partners = await _businessPartnerRepository.GetQueryableAsync();
        var counts = await GetCountsAsync(products, documents, today);
        var alerts = await GetAlertWindowAsync(products, documents, partners, today, 4);

        return new OperationalAlertsSummaryDto
        {
            TotalCount = counts.Total,
            CriticalCount = counts.Critical,
            WarningCount = counts.Total - counts.Critical,
            Alerts = alerts.Take(4).ToList()
        };
    }

    public async Task<OperationalAlertsPageDto> GetPageAsync(OperationalAlertGetListInput input)
    {
        CurrentTenant.RequireTenantId();
        var today = Clock.Now.Date;
        var maxResultCount = Math.Clamp(input.MaxResultCount, 1, 50);
        var skipCount = Math.Clamp(input.SkipCount, 0, 10_000);
        var windowSize = Math.Min(skipCount + maxResultCount, 10_050);
        var products = await _productRepository.GetQueryableAsync();
        var documents = await _documentRepository.GetQueryableAsync();
        var partners = await _businessPartnerRepository.GetQueryableAsync();
        var counts = await GetCountsAsync(products, documents, today);
        var alerts = await GetAlertWindowAsync(products, documents, partners, today, windowSize);

        return new OperationalAlertsPageDto
        {
            TotalCount = counts.Total,
            CriticalCount = counts.Critical,
            WarningCount = counts.Total - counts.Critical,
            SkipCount = skipCount,
            MaxResultCount = maxResultCount,
            Alerts = alerts.Skip(skipCount).Take(maxResultCount).ToList()
        };
    }

    private async Task<AlertCounts> GetCountsAsync(
        IQueryable<Product> products,
        IQueryable<Document> documents,
        DateTime today)
    {
        var productCounts = await AsyncExecuter.FirstOrDefaultAsync(products
            .Where(x => x.TrackStock && x.Quantity <= x.CriticalStockLevel)
            .GroupBy(_ => 1)
            .Select(group => new AlertCounts
            {
                Total = group.Count(),
                Critical = group.Count(x => x.Quantity <= 0)
            }));

        var dueDocuments = GetDueDocumentsQuery(documents, today);
        var documentCounts = await AsyncExecuter.FirstOrDefaultAsync(dueDocuments
            .GroupBy(_ => 1)
            .Select(group => new AlertCounts
            {
                Total = group.Count(),
                Critical = group.Count(x => x.DueDate!.Value < today)
            }));

        return new AlertCounts
        {
            Total = (productCounts?.Total ?? 0) + (documentCounts?.Total ?? 0),
            Critical = (productCounts?.Critical ?? 0) + (documentCounts?.Critical ?? 0)
        };
    }

    private async Task<System.Collections.Generic.List<OperationalAlertDto>> GetAlertWindowAsync(
        IQueryable<Product> products,
        IQueryable<Document> documents,
        IQueryable<BusinessPartner> partners,
        DateTime today,
        int windowSize)
    {
        if (windowSize <= 0)
        {
            return new System.Collections.Generic.List<OperationalAlertDto>();
        }

        var criticalProducts = await AsyncExecuter.ToListAsync(products
            .Where(x => x.TrackStock && x.Quantity <= x.CriticalStockLevel)
            .OrderByDescending(x => x.Quantity <= 0)
            .ThenBy(x => x.Quantity)
            .ThenBy(x => x.Name)
            .Take(windowSize)
            .Select(product => new CriticalProductAlertRow
            {
                Name = product.Name,
                Unit = product.Unit,
                Quantity = product.Quantity,
                CriticalStockLevel = product.CriticalStockLevel
            }));

        var alerts = criticalProducts.Select(product => new OperationalAlertDto
        {
            Type = "CriticalStock",
            Severity = product.Quantity <= 0 ? "Critical" : "Warning",
            Title = product.Quantity <= 0 ? "Stok tükendi" : "Kritik stok seviyesi",
            Message = $"{product.Name} için mevcut stok {product.Quantity:0.##} {product.Unit}; kritik seviye {product.CriticalStockLevel:0.##}.",
            Link = "/Products",
            OccurredAt = today
        }).ToList();

        var dueDocumentsQuery =
            from document in documents
            join partner in partners on document.BusinessPartnerId equals partner.Id
            where (document.Type == DocumentType.SaleInvoice || document.Type == DocumentType.PurchaseInvoice) &&
                  (document.Status == DocumentStatus.Approved || document.Status == DocumentStatus.PartiallyPaid) &&
                  document.DueDate.HasValue &&
                  document.GrandTotal > document.PaidAmount &&
                  document.DueDate.Value < today.AddDays(8)
            orderby document.DueDate < today descending, document.DueDate ascending, document.DocNumber
            select new DueDocumentAlertRow
            {
                DocNumber = document.DocNumber,
                Type = document.Type,
                DueDate = document.DueDate!.Value,
                PartnerName = partner.Name
            };
        var dueDocuments = await AsyncExecuter.ToListAsync(dueDocumentsQuery.Take(windowSize));

        alerts.AddRange(dueDocuments.Select(row =>
        {
            var dueDate = row.DueDate.Date;
            var overdue = dueDate < today;
            var days = Math.Abs((dueDate - today).Days);
            var direction = row.Type == DocumentType.SaleInvoice ? "tahsilatı" : "ödemesi";
            return new OperationalAlertDto
            {
                Type = "DueDocument",
                Severity = overdue ? "Critical" : "Warning",
                Title = overdue ? "Vadesi geçmiş belge" : "Yaklaşan vade",
                Message = $"{row.DocNumber} - {row.PartnerName} belgesinin {direction} {(overdue ? $"{days} gün gecikti" : $"{days} gün içinde") }.",
                Link = "/Invoices",
                OccurredAt = dueDate
            };
        }));

        return alerts
            .OrderByDescending(x => x.Severity == "Critical")
            .ThenBy(x => x.OccurredAt)
            .ThenBy(x => x.Title)
            .Take(windowSize)
            .ToList();
    }

    private static IQueryable<Document> GetDueDocumentsQuery(IQueryable<Document> documents, DateTime today)
    {
        var dueSoon = today.AddDays(8);
        return documents.Where(document =>
            (document.Type == DocumentType.SaleInvoice || document.Type == DocumentType.PurchaseInvoice) &&
            (document.Status == DocumentStatus.Approved || document.Status == DocumentStatus.PartiallyPaid) &&
            document.DueDate.HasValue &&
            document.GrandTotal > document.PaidAmount &&
            document.DueDate.Value < dueSoon);
    }

    private sealed class AlertCounts
    {
        public int Total { get; set; }
        public int Critical { get; set; }
    }

    private sealed class CriticalProductAlertRow
    {
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal CriticalStockLevel { get; set; }
    }

    private sealed class DueDocumentAlertRow
    {
        public string DocNumber { get; set; } = string.Empty;
        public DocumentType Type { get; set; }
        public DateTime DueDate { get; set; }
        public string PartnerName { get; set; } = string.Empty;
    }
}
