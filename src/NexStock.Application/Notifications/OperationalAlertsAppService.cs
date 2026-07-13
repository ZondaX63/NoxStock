using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NexStock.BusinessPartners;
using NexStock.Documents;
using NexStock.Permissions;
using NexStock.Products;
using NexStock.MultiTenancy;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace NexStock.Notifications;

[Authorize(NexStockPermissions.Notifications.Default)]
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
        var alerts = new System.Collections.Generic.List<OperationalAlertDto>();
        var products = await _productRepository.GetQueryableAsync();
        var documents = await _documentRepository.GetQueryableAsync();
        var partners = await _businessPartnerRepository.GetQueryableAsync();

        var criticalProducts = await AsyncExecuter.ToListAsync(products
            .Where(x => x.TrackStock && x.Quantity <= x.CriticalStockLevel)
            .OrderBy(x => x.Quantity)
            .ThenBy(x => x.Name)
            .Take(50));

        alerts.AddRange(criticalProducts.Select(product => new OperationalAlertDto
        {
            Type = "CriticalStock",
            Severity = product.Quantity <= 0 ? "Critical" : "Warning",
            Title = product.Quantity <= 0 ? "Stok tükendi" : "Kritik stok seviyesi",
            Message = $"{product.Name} için mevcut stok {product.Quantity:0.##} {product.Unit}; kritik seviye {product.CriticalStockLevel:0.##}.",
            Link = "/Products",
            OccurredAt = today
        }));

        var dueDocuments = await AsyncExecuter.ToListAsync(
            from document in documents
            join partner in partners on document.BusinessPartnerId equals partner.Id
            where (document.Type == DocumentType.SaleInvoice || document.Type == DocumentType.PurchaseInvoice) &&
                  (document.Status == DocumentStatus.Approved || document.Status == DocumentStatus.PartiallyPaid) &&
                  document.DueDate.HasValue &&
                  document.GrandTotal > document.PaidAmount &&
                  document.DueDate.Value.Date <= today.AddDays(7)
            orderby document.DueDate ascending
            select new
            {
                Document = document,
                PartnerName = partner.Name
            });

        alerts.AddRange(dueDocuments.Select(row =>
        {
            var dueDate = row.Document.DueDate!.Value.Date;
            var overdue = dueDate < today;
            var days = Math.Abs((dueDate - today).Days);
            var direction = row.Document.Type == DocumentType.SaleInvoice ? "tahsilatı" : "ödemesi";
            return new OperationalAlertDto
            {
                Type = "DueDocument",
                Severity = overdue ? "Critical" : "Warning",
                Title = overdue ? "Vadesi geçmiş belge" : "Yaklaşan vade",
                Message = $"{row.Document.DocNumber} - {row.PartnerName} belgesinin {direction} {(overdue ? $"{days} gün gecikti" : $"{days} gün içinde") }.",
                Link = "/Invoices",
                OccurredAt = dueDate
            };
        }));

        return new OperationalAlertsSummaryDto
        {
            TotalCount = alerts.Count,
            CriticalCount = alerts.Count(x => x.Severity == "Critical"),
            WarningCount = alerts.Count(x => x.Severity == "Warning"),
            Alerts = alerts
                .OrderByDescending(x => x.Severity == "Critical")
                .ThenBy(x => x.OccurredAt)
                .ToList()
        };
    }
}
