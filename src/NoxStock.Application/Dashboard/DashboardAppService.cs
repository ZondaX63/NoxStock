using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.Accounts;
using NoxStock.BusinessPartners;
using NoxStock.Documents;
using NoxStock.Products;
using NoxStock.MultiTenancy;
using NoxStock.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace NoxStock.Dashboard;

[Authorize(NoxStockPermissions.Dashboard.Default)]
public class DashboardAppService : ApplicationService, IDashboardAppService
{
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<DocumentItem, Guid> _documentItemRepository;
    private readonly IRepository<Product, Guid> _productRepository;

    public DashboardAppService(
        IRepository<Account, Guid> accountRepository,
        IRepository<BusinessPartner, Guid> businessPartnerRepository,
        IRepository<Document, Guid> documentRepository,
        IRepository<DocumentItem, Guid> documentItemRepository,
        IRepository<Product, Guid> productRepository)
    {
        _accountRepository = accountRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _documentRepository = documentRepository;
        _documentItemRepository = documentItemRepository;
        _productRepository = productRepository;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        CurrentTenant.RequireTenantId();
        var today = Clock.Now.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var trendStart = monthStart.AddMonths(-5);
        var nextMonthStart = monthStart.AddMonths(1);
        var tomorrow = today.AddDays(1);
        var dueSoonDate = today.AddDays(7);

        var accounts = await _accountRepository.GetQueryableAsync();
        var businessPartners = await _businessPartnerRepository.GetQueryableAsync();
        var documents = await _documentRepository.GetQueryableAsync();
        var documentItems = await _documentItemRepository.GetQueryableAsync();
        var products = await _productRepository.GetQueryableAsync();

        var saleInvoices = documents.Where(x =>
            (x.Type == DocumentType.SaleInvoice || x.Type == DocumentType.PosSale) &&
            (x.Status == DocumentStatus.Approved ||
             x.Status == DocumentStatus.PartiallyPaid ||
             x.Status == DocumentStatus.Paid ||
             x.Status == DocumentStatus.Completed));

        var documentMetrics = await AsyncExecuter.FirstOrDefaultAsync(documents
            .GroupBy(_ => 1)
            .Select(group => new DashboardDocumentMetrics
            {
                TodaySales = group
                    .Where(x =>
                        (x.Type == DocumentType.SaleInvoice || x.Type == DocumentType.PosSale) &&
                        (x.Status == DocumentStatus.Approved ||
                         x.Status == DocumentStatus.PartiallyPaid ||
                         x.Status == DocumentStatus.Paid ||
                         x.Status == DocumentStatus.Completed) &&
                        x.Date >= today && x.Date < tomorrow)
                    .Sum(x => x.GrandTotal),
                MonthlyRevenue = group
                    .Where(x =>
                        (x.Type == DocumentType.SaleInvoice || x.Type == DocumentType.PosSale) &&
                        (x.Status == DocumentStatus.Approved ||
                         x.Status == DocumentStatus.PartiallyPaid ||
                         x.Status == DocumentStatus.Paid ||
                         x.Status == DocumentStatus.Completed) &&
                        x.Date >= monthStart && x.Date < nextMonthStart)
                    .Sum(x => x.GrandTotal),
                PendingInvoiceCount = group.Count(x =>
                    (x.Type == DocumentType.SaleInvoice || x.Type == DocumentType.PurchaseInvoice) &&
                    (x.Status == DocumentStatus.Draft ||
                     x.Status == DocumentStatus.Sent))
            })) ?? new DashboardDocumentMetrics();

        var totalAccountBalance = await AsyncExecuter.SumAsync(accounts.Select(x => x.Balance));
        var partnerMetrics = await AsyncExecuter.FirstOrDefaultAsync(businessPartners
            .GroupBy(_ => 1)
            .Select(group => new DashboardPartnerMetrics
            {
                CustomerReceivables = group.Where(x => x.Balance > 0).Sum(x => x.Balance),
                SupplierPayables = group.Where(x => x.Balance < 0).Sum(x => -x.Balance),
                ActiveCustomerCount = group.Count(x =>
                    x.Type == BusinessPartnerType.Customer || x.Type == BusinessPartnerType.Both)
            })) ?? new DashboardPartnerMetrics();

        var criticalStockCount = await AsyncExecuter.CountAsync(products
            .Where(x => x.TrackStock && x.Quantity <= x.CriticalStockLevel));

        var recentDocumentsQuery =
            from document in documents
            join businessPartner in businessPartners on document.BusinessPartnerId equals businessPartner.Id
            orderby document.Date descending, document.CreationTime descending
            select new DashboardDocumentSummaryDto
            {
                Id = document.Id,
                DocNumber = document.DocNumber,
                BusinessPartnerName = businessPartner.Name,
                Type = document.Type,
                Status = document.Status,
                GrandTotal = document.GrandTotal,
                Date = document.Date
            };

        var criticalStocksQuery = products
            .Where(x => x.TrackStock && x.Quantity <= x.CriticalStockLevel)
            .OrderBy(x => x.Quantity)
            .ThenBy(x => x.Name)
            .Select(x => new DashboardCriticalStockDto
            {
                Id = x.Id,
                Name = x.Name,
                Sku = x.Sku,
                Unit = x.Unit,
                Quantity = x.Quantity,
                CriticalStockLevel = x.CriticalStockLevel
            });

        var trendRows = await AsyncExecuter.ToListAsync(saleInvoices
            .Where(x => x.Date >= trendStart && x.Date < nextMonthStart)
            .GroupBy(x => new { x.Date.Year, x.Date.Month })
            .Select(x => new
            {
                x.Key.Year,
                x.Key.Month,
                Total = x.Sum(y => y.GrandTotal)
            }));

        var salesTrend = Enumerable.Range(0, 6)
            .Select(offset => monthStart.AddMonths(offset - 5))
            .Select(month => new DashboardSalesTrendDto
            {
                Label = month.ToString("MMM", System.Globalization.CultureInfo.GetCultureInfo("tr-TR")),
                Total = trendRows
                    .Where(x => x.Year == month.Year && x.Month == month.Month)
                    .Select(x => x.Total)
                    .FirstOrDefault()
            })
            .ToList();

        var topProductsQuery =
            from item in documentItems
            join document in saleInvoices on item.DocumentId equals document.Id
            join product in products on item.ProductId equals product.Id
            group item by new { product.Id, product.Name, product.Sku } into productGroup
            orderby productGroup.Sum(x => x.Total) descending
            select new DashboardTopProductDto
            {
                ProductId = productGroup.Key.Id,
                Name = productGroup.Key.Name,
                Sku = productGroup.Key.Sku,
                Quantity = productGroup.Sum(x => x.Quantity),
                Revenue = productGroup.Sum(x => x.Total)
            };

        var dueDocumentsQuery =
            from document in documents
            join businessPartner in businessPartners on document.BusinessPartnerId equals businessPartner.Id
            where (document.Type == DocumentType.SaleInvoice || document.Type == DocumentType.PurchaseInvoice) &&
                  (document.Status == DocumentStatus.Approved || document.Status == DocumentStatus.PartiallyPaid) &&
                  document.DueDate.HasValue &&
                  document.DueDate.Value <= dueSoonDate
            orderby document.DueDate ascending, document.Date ascending
            select new DashboardDueDocumentDto
            {
                Id = document.Id,
                DocNumber = document.DocNumber,
                BusinessPartnerName = businessPartner.Name,
                Type = document.Type,
                DueDate = document.DueDate!.Value,
                OutstandingAmount = document.GrandTotal - document.PaidAmount,
                Currency = document.Currency
            };

        return new DashboardSummaryDto
        {
            TodaySales = documentMetrics.TodaySales,
            MonthlyRevenue = documentMetrics.MonthlyRevenue,
            TotalAccountBalance = totalAccountBalance,
            CustomerReceivables = partnerMetrics.CustomerReceivables,
            SupplierPayables = partnerMetrics.SupplierPayables,
            NetPosition = totalAccountBalance + partnerMetrics.CustomerReceivables - partnerMetrics.SupplierPayables,
            CriticalStockCount = criticalStockCount,
            PendingInvoiceCount = documentMetrics.PendingInvoiceCount,
            ActiveCustomerCount = partnerMetrics.ActiveCustomerCount,
            RecentDocuments = await AsyncExecuter.ToListAsync(recentDocumentsQuery.Take(5)),
            CriticalStocks = await AsyncExecuter.ToListAsync(criticalStocksQuery.Take(5)),
            SalesTrend = salesTrend,
            TopProducts = await AsyncExecuter.ToListAsync(topProductsQuery.Take(5)),
            DueDocuments = await AsyncExecuter.ToListAsync(dueDocumentsQuery.Take(5))
        };
    }

    private sealed class DashboardDocumentMetrics
    {
        public decimal TodaySales { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public int PendingInvoiceCount { get; set; }
    }

    private sealed class DashboardPartnerMetrics
    {
        public decimal CustomerReceivables { get; set; }
        public decimal SupplierPayables { get; set; }
        public int ActiveCustomerCount { get; set; }
    }
}
