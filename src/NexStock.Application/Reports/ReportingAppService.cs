using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NexStock.Accounts;
using NexStock.BusinessPartners;
using NexStock.Documents;
using NexStock.Permissions;
using NexStock.Products;
using NexStock.StockMovements;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace NexStock.Reports;

[Authorize(NexStockPermissions.Reports.Default)]
public class ReportingAppService : ApplicationService, IReportingAppService
{
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<DocumentItem, Guid> _documentItemRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<AccountTransaction, Guid> _accountTransactionRepository;
    private readonly IRepository<StockMovement, Guid> _stockMovementRepository;
    private readonly IProductPriceCalculator _priceCalculator;

    public ReportingAppService(
        IRepository<Document, Guid> documentRepository,
        IRepository<DocumentItem, Guid> documentItemRepository,
        IRepository<Product, Guid> productRepository,
        IRepository<BusinessPartner, Guid> businessPartnerRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<AccountTransaction, Guid> accountTransactionRepository,
        IRepository<StockMovement, Guid> stockMovementRepository,
        IProductPriceCalculator priceCalculator)
    {
        _documentRepository = documentRepository;
        _documentItemRepository = documentItemRepository;
        _productRepository = productRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _accountRepository = accountRepository;
        _accountTransactionRepository = accountTransactionRepository;
        _stockMovementRepository = stockMovementRepository;
        _priceCalculator = priceCalculator;
    }

    public async Task<ReportSnapshotDto> GetSnapshotAsync(ReportRequestDto input)
    {
        var dateTo = (input.DateTo?.Date ?? Clock.Now.Date).AddDays(1);
        var dateFrom = input.DateFrom?.Date ?? dateTo.AddMonths(-1);
        if (dateFrom >= dateTo)
        {
            dateFrom = dateTo.AddDays(-1);
        }

        var documents = await AsyncExecuter.ToListAsync((await _documentRepository.GetQueryableAsync())
            .Where(x => x.Date >= dateFrom && x.Date < dateTo));
        var products = await AsyncExecuter.ToListAsync(await _productRepository.GetQueryableAsync());
        var partners = await AsyncExecuter.ToListAsync(await _businessPartnerRepository.GetQueryableAsync());
        var accounts = await AsyncExecuter.ToListAsync(await _accountRepository.GetQueryableAsync());
        var transactions = await AsyncExecuter.ToListAsync((await _accountTransactionRepository.GetQueryableAsync())
            .Where(x => x.TransactionDate >= dateFrom && x.TransactionDate < dateTo));
        var movements = await AsyncExecuter.ToListAsync((await _stockMovementRepository.GetQueryableAsync())
            .Where(x => x.MovementDate >= dateFrom && x.MovementDate < dateTo));

        var completedDocuments = documents.Where(IsCommerciallyCompleted).ToList();
        var completedDocumentIds = completedDocuments.Select(x => x.Id).ToList();
        var documentItems = completedDocumentIds.Count == 0
            ? new List<DocumentItem>()
            : await AsyncExecuter.ToListAsync((await _documentItemRepository.GetQueryableAsync())
                .Where(x => completedDocumentIds.Contains(x.DocumentId)));

        var salesDocuments = completedDocuments.Where(x => x.Type is DocumentType.SaleInvoice or DocumentType.PosSale).ToList();
        var purchaseDocuments = completedDocuments.Where(x => x.Type == DocumentType.PurchaseInvoice).ToList();
        var salesDocumentIds = salesDocuments.Select(x => x.Id).ToHashSet();
        var purchaseDocumentIds = purchaseDocuments.Select(x => x.Id).ToHashSet();
        var documentsById = completedDocuments.ToDictionary(x => x.Id);
        var productsById = products.ToDictionary(x => x.Id);
        var costInTryByProductId = new Dictionary<Guid, decimal>();
        foreach (var product in products)
        {
            var purchaseCurrency = string.IsNullOrWhiteSpace(product.PurchaseCurrency) ? "TRY" : product.PurchaseCurrency;
            costInTryByProductId[product.Id] = product.PurchasePrice * await _priceCalculator.GetExchangeRateAsync(purchaseCurrency);
        }

        var stockRows = products
            .OrderBy(x => x.Name)
            .Select(x => new StockReportItemDto
            {
                ProductId = x.Id,
                Name = x.Name,
                Sku = x.Sku,
                Unit = x.Unit,
                Quantity = x.Quantity,
                CriticalStockLevel = x.CriticalStockLevel,
                PurchasePrice = costInTryByProductId[x.Id],
                CostValue = x.Quantity * costInTryByProductId[x.Id],
                IsCritical = x.TrackStock && x.Quantity <= x.CriticalStockLevel
            })
            .ToList();

        var movementRows = movements
            .OrderByDescending(x => x.MovementDate)
            .Take(100)
            .Select(x => new StockMovementReportItemDto
            {
                Date = x.MovementDate,
                ProductName = productsById.TryGetValue(x.ProductId, out var product) ? product.Name : "Silinmiş ürün",
                Type = x.Type,
                Quantity = x.Quantity,
                BalanceAfter = x.BalanceAfter,
                Description = x.Description
            })
            .ToList();

        var accountRows = accounts
            .OrderBy(x => x.Name)
            .Select(account =>
            {
                var accountTransactions = transactions.Where(x => x.AccountId == account.Id).ToList();
                var signed = accountTransactions.Select(GetSignedAmount).ToList();
                return new AccountReportItemDto
                {
                    AccountId = account.Id,
                    Name = account.Name,
                    Type = account.Type,
                    Currency = account.Currency,
                    Balance = account.Balance,
                    Inflow = signed.Where(x => x > 0).Sum(),
                    Outflow = -signed.Where(x => x < 0).Sum(),
                    NetFlow = signed.Sum()
                };
            })
            .ToList();

        var partnerRows = partners
            .OrderBy(x => x.Name)
            .Select(partner => new BusinessPartnerReportItemDto
            {
                BusinessPartnerId = partner.Id,
                Name = partner.Name,
                Type = partner.Type,
                Currency = partner.Currency,
                Balance = partner.Balance,
                Sales = salesDocuments.Where(x => x.BusinessPartnerId == partner.Id).Sum(GetReportAmount),
                Purchases = purchaseDocuments.Where(x => x.BusinessPartnerId == partner.Id).Sum(GetReportAmount)
            })
            .Where(x => x.Balance != 0 || x.Sales != 0 || x.Purchases != 0)
            .ToList();

        var vatRows = documentItems
            .GroupBy(item => item.VatRate)
            .OrderBy(x => x.Key)
            .Select(group =>
            {
                var salesVat = group.Where(x => salesDocumentIds.Contains(x.DocumentId)).Sum(item => GetVatAmount(item) * GetDocumentRate(documentsById[item.DocumentId]));
                var purchaseVat = group.Where(x => purchaseDocumentIds.Contains(x.DocumentId)).Sum(item => GetVatAmount(item) * GetDocumentRate(documentsById[item.DocumentId]));
                return new VatReportItemDto
                {
                    VatRate = group.Key,
                    SalesVat = salesVat,
                    PurchaseVat = purchaseVat,
                    NetVat = salesVat - purchaseVat
                };
            })
            .ToList();

        var profitabilityRows = documentItems
            .Where(x => salesDocumentIds.Contains(x.DocumentId))
            .GroupBy(x => x.ProductId)
            .Select(group =>
            {
                productsById.TryGetValue(group.Key, out var product);
                var salesAmount = group.Sum(x => x.Total * GetDocumentRate(documentsById[x.DocumentId]));
                var estimatedCost = group.Sum(x => x.Quantity * (costInTryByProductId.TryGetValue(x.ProductId, out var cost) ? cost : 0));
                return new ProductProfitabilityReportItemDto
                {
                    ProductId = group.Key,
                    Name = product?.Name ?? "Silinmiş ürün",
                    Sku = product?.Sku ?? "-",
                    SoldQuantity = group.Sum(x => x.Quantity),
                    SalesAmount = salesAmount,
                    EstimatedCost = estimatedCost,
                    GrossProfitEstimate = salesAmount - estimatedCost,
                    GrossMarginPercent = salesAmount == 0 ? 0 : Math.Round((salesAmount - estimatedCost) / salesAmount * 100, 2)
                };
            })
            .OrderByDescending(x => x.GrossProfitEstimate)
            .Take(50)
            .ToList();

        var aging = CreateAgingRows(await GetOpenInvoicesAsync(), Clock.Now.Date);
        var signedTransactions = transactions.Select(GetSignedAmount).ToList();
        var income = transactions.Where(x => x.Type == AccountTransactionType.Income).Sum(x => Math.Abs(x.Amount));
        var expense = transactions.Where(x => x.Type == AccountTransactionType.Expense).Sum(x => Math.Abs(x.Amount));

        return new ReportSnapshotDto
        {
            DateFrom = dateFrom,
            DateTo = dateTo.AddDays(-1),
            Summary = new ReportSummaryDto
            {
                Sales = salesDocuments.Sum(GetReportAmount),
                Purchases = purchaseDocuments.Sum(GetReportAmount),
                GrossProfitEstimate = profitabilityRows.Sum(x => x.GrossProfitEstimate),
                CashIn = signedTransactions.Where(x => x > 0).Sum(),
                CashOut = -signedTransactions.Where(x => x < 0).Sum(),
                NetCashFlow = signedTransactions.Sum(),
                Income = income,
                Expense = expense,
                Receivables = partners.Where(x => x.Balance > 0).Sum(x => x.Balance),
                Payables = partners.Where(x => x.Balance < 0).Sum(x => -x.Balance),
                InventoryCostValue = stockRows.Sum(x => x.CostValue),
                CriticalStockCount = stockRows.Count(x => x.IsCritical)
            },
            Stocks = stockRows,
            StockMovements = movementRows,
            Accounts = accountRows,
            BusinessPartners = partnerRows,
            VatRates = vatRows,
            Aging = aging,
            ProductProfitability = profitabilityRows
        };
    }

    private async Task<List<Document>> GetOpenInvoicesAsync()
    {
        return await AsyncExecuter.ToListAsync((await _documentRepository.GetQueryableAsync())
            .Where(x => (x.Type == DocumentType.SaleInvoice || x.Type == DocumentType.PurchaseInvoice) &&
                        (x.Status == DocumentStatus.Approved || x.Status == DocumentStatus.PartiallyPaid)));
    }

    private static bool IsCommerciallyCompleted(Document document)
    {
        return document.Status is DocumentStatus.Approved or DocumentStatus.PartiallyPaid or DocumentStatus.Paid or DocumentStatus.Completed;
    }

    private static decimal GetVatAmount(DocumentItem item)
    {
        var net = item.Quantity * item.UnitPrice * (1 - item.DiscountRate / 100);
        return net * item.VatRate / 100;
    }

    private static decimal GetDocumentRate(Document document)
    {
        return string.Equals(document.Currency, "TRY", StringComparison.OrdinalIgnoreCase) || document.ExchangeRate <= 0
            ? 1
            : document.ExchangeRate;
    }

    private static decimal GetReportAmount(Document document)
    {
        return document.GrandTotal * GetDocumentRate(document);
    }

    private static decimal GetReportOutstandingAmount(Document document)
    {
        return (document.GrandTotal - document.PaidAmount) * GetDocumentRate(document);
    }

    private static decimal GetSignedAmount(AccountTransaction transaction)
    {
        var amount = Math.Abs(transaction.Amount);
        return transaction.Type switch
        {
            AccountTransactionType.Opening or AccountTransactionType.Income or AccountTransactionType.TransferIn or AccountTransactionType.Collection => amount,
            AccountTransactionType.Expense or AccountTransactionType.TransferOut or AccountTransactionType.Payment => -amount,
            _ => transaction.Amount
        };
    }

    private static List<AgingReportItemDto> CreateAgingRows(List<Document> documents, DateTime today)
    {
        var buckets = new[] { "Vadesi gelmemiş", "0-30 gün", "31-60 gün", "61-90 gün", "90+ gün" };
        return buckets.Select(bucket => new AgingReportItemDto
        {
            Bucket = bucket,
            Receivables = documents.Where(x => x.Type == DocumentType.SaleInvoice && GetAgingBucket(x.DueDate, today) == bucket)
                .Sum(GetReportOutstandingAmount),
            Payables = documents.Where(x => x.Type == DocumentType.PurchaseInvoice && GetAgingBucket(x.DueDate, today) == bucket)
                .Sum(GetReportOutstandingAmount)
        }).ToList();
    }

    private static string GetAgingBucket(DateTime? dueDate, DateTime today)
    {
        if (!dueDate.HasValue || dueDate.Value.Date >= today)
        {
            return "Vadesi gelmemiş";
        }

        var days = (today - dueDate.Value.Date).Days;
        return days <= 30 ? "0-30 gün" : days <= 60 ? "31-60 gün" : days <= 90 ? "61-90 gün" : "90+ gün";
    }
}
