using System;
using System.Linq;
using System.Threading.Tasks;
using NexStock.Accounts;
using NexStock.BusinessPartners;
using NexStock.Documents;
using NexStock.Products;
using NexStock.MultiTenancy;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace NexStock.Dashboard;

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

        var todaySales = await AsyncExecuter.SumAsync(saleInvoices
            .Where(x => x.Date.Date == today)
            .Select(x => x.GrandTotal));

        var monthlyRevenue = await AsyncExecuter.SumAsync(saleInvoices
            .Where(x => x.Date >= monthStart)
            .Select(x => x.GrandTotal));

        var totalAccountBalance = await AsyncExecuter.SumAsync(accounts.Select(x => x.Balance));
        var customerReceivables = await AsyncExecuter.SumAsync(businessPartners
            .Where(x => x.Balance > 0)
            .Select(x => x.Balance));
        var supplierPayables = await AsyncExecuter.SumAsync(businessPartners
            .Where(x => x.Balance < 0)
            .Select(x => -x.Balance));

        var criticalStockCount = await AsyncExecuter.CountAsync(products
            .Where(x => x.TrackStock && x.Quantity <= x.CriticalStockLevel));

        var pendingInvoiceCount = await AsyncExecuter.CountAsync(documents
            .Where(x =>
                (x.Type == DocumentType.SaleInvoice || x.Type == DocumentType.PurchaseInvoice) &&
                (x.Status == DocumentStatus.Draft ||
                 x.Status == DocumentStatus.Sent ||
                 x.Status == DocumentStatus.Approved)));

        var activeCustomerCount = await AsyncExecuter.CountAsync(businessPartners
            .Where(x => x.Type == BusinessPartnerType.Customer || x.Type == BusinessPartnerType.Both));

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
            TodaySales = todaySales,
            MonthlyRevenue = monthlyRevenue,
            TotalAccountBalance = totalAccountBalance,
            CustomerReceivables = customerReceivables,
            SupplierPayables = supplierPayables,
            NetPosition = totalAccountBalance + customerReceivables - supplierPayables,
            CriticalStockCount = criticalStockCount,
            PendingInvoiceCount = pendingInvoiceCount,
            ActiveCustomerCount = activeCustomerCount,
            RecentDocuments = await AsyncExecuter.ToListAsync(recentDocumentsQuery.Take(5)),
            CriticalStocks = await AsyncExecuter.ToListAsync(criticalStocksQuery.Take(5)),
            SalesTrend = salesTrend,
            TopProducts = await AsyncExecuter.ToListAsync(topProductsQuery.Take(5)),
            DueDocuments = await AsyncExecuter.ToListAsync(dueDocumentsQuery.Take(5))
        };
    }
}
