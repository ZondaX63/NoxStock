using System;
using System.Linq;
using System.Threading.Tasks;
using NexStock.Accounts;
using NexStock.BusinessPartners;
using NexStock.Documents;
using NexStock.Products;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace NexStock.Dashboard;

public class DashboardAppService : ApplicationService, IDashboardAppService
{
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<Product, Guid> _productRepository;

    public DashboardAppService(
        IRepository<Account, Guid> accountRepository,
        IRepository<BusinessPartner, Guid> businessPartnerRepository,
        IRepository<Document, Guid> documentRepository,
        IRepository<Product, Guid> productRepository)
    {
        _accountRepository = accountRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _documentRepository = documentRepository;
        _productRepository = productRepository;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        var today = Clock.Now.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var accounts = await _accountRepository.GetQueryableAsync();
        var businessPartners = await _businessPartnerRepository.GetQueryableAsync();
        var documents = await _documentRepository.GetQueryableAsync();
        var products = await _productRepository.GetQueryableAsync();

        var saleInvoices = documents.Where(x =>
            x.Type == DocumentType.SaleInvoice &&
            x.Status != DocumentStatus.Cancelled &&
            x.Status != DocumentStatus.Rejected);

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
            RecentDocuments = await AsyncExecuter.ToListAsync(recentDocumentsQuery.Take(5))
        };
    }
}

