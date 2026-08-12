using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.Accounts;
using NoxStock.BusinessPartners;
using NoxStock.Documents;
using NoxStock.MultiTenancy;
using NoxStock.Permissions;
using NoxStock.Products;
using NoxStock.StockMovements;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace NoxStock.Reports;

[Authorize(NoxStockPermissions.Reports.Default)]
public class ReportingAppService : ApplicationService, IReportingAppService
{
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<DocumentItem, Guid> _documentItemRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<AccountTransaction, Guid> _accountTransactionRepository;
    private readonly IRepository<StockMovement, Guid> _stockMovementRepository;

    public ReportingAppService(
        IRepository<Document, Guid> documentRepository,
        IRepository<DocumentItem, Guid> documentItemRepository,
        IRepository<Product, Guid> productRepository,
        IRepository<BusinessPartner, Guid> businessPartnerRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<AccountTransaction, Guid> accountTransactionRepository,
        IRepository<StockMovement, Guid> stockMovementRepository)
    {
        _documentRepository = documentRepository;
        _documentItemRepository = documentItemRepository;
        _productRepository = productRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _accountRepository = accountRepository;
        _accountTransactionRepository = accountTransactionRepository;
        _stockMovementRepository = stockMovementRepository;
    }

    public async Task<ReportSnapshotDto> GetSnapshotAsync(ReportRequestDto input)
    {
        CurrentTenant.RequireTenantId();
        var dateToExclusive = (input.DateTo?.Date ?? Clock.Now.Date).AddDays(1);
        var dateFrom = input.DateFrom?.Date ?? dateToExclusive.AddMonths(-1);
        if (dateFrom >= dateToExclusive)
        {
            dateFrom = dateToExclusive.AddDays(-1);
        }
        if ((dateToExclusive - dateFrom).TotalDays > 366)
        {
            dateFrom = dateToExclusive.AddDays(-366);
        }

        var pageSize = Math.Clamp(input.PageSize, 10, 100);
        var accountsPage = NormalizePage(input.AccountsPage);
        var partnersPage = NormalizePage(input.BusinessPartnersPage);
        var profitabilityPage = NormalizePage(input.ProductProfitabilityPage);
        var stocksPage = NormalizePage(input.StocksPage);
        var movementsPage = NormalizePage(input.StockMovementsPage);

        var documents = await _documentRepository.GetQueryableAsync();
        var documentItems = await _documentItemRepository.GetQueryableAsync();
        var products = await _productRepository.GetQueryableAsync();
        var partners = await _businessPartnerRepository.GetQueryableAsync();
        var accounts = await _accountRepository.GetQueryableAsync();
        var accountTransactions = await _accountTransactionRepository.GetQueryableAsync();
        var stockMovements = await _stockMovementRepository.GetQueryableAsync();

        var completedDocuments = documents.Where(document =>
            document.Date >= dateFrom && document.Date < dateToExclusive &&
            (document.Status == DocumentStatus.Approved ||
             document.Status == DocumentStatus.PartiallyPaid ||
             document.Status == DocumentStatus.Paid ||
             document.Status == DocumentStatus.Completed));
        var salesDocuments = completedDocuments.Where(document =>
            document.Type == DocumentType.SaleInvoice || document.Type == DocumentType.PosSale);
        var purchaseDocuments = completedDocuments.Where(document =>
            document.Type == DocumentType.PurchaseInvoice);
        var periodTransactions = accountTransactions.Where(transaction =>
            transaction.TransactionDate >= dateFrom && transaction.TransactionDate < dateToExclusive);
        var periodMovements = stockMovements.Where(movement =>
            movement.MovementDate >= dateFrom && movement.MovementDate < dateToExclusive);

        var documentSummary = await AsyncExecuter.FirstOrDefaultAsync(completedDocuments
            .GroupBy(_ => 1)
            .Select(group => new DocumentSummaryRow
            {
                Sales = group
                    .Where(document => document.Type == DocumentType.SaleInvoice || document.Type == DocumentType.PosSale)
                    .Sum(document => document.GrandTotal *
                        (document.Currency == "TRY" || document.ExchangeRate <= 0 ? 1m : document.ExchangeRate)),
                Purchases = group
                    .Where(document => document.Type == DocumentType.PurchaseInvoice)
                    .Sum(document => document.GrandTotal *
                        (document.Currency == "TRY" || document.ExchangeRate <= 0 ? 1m : document.ExchangeRate))
            })) ?? new DocumentSummaryRow();

        var transactionSummary = await AsyncExecuter.FirstOrDefaultAsync(periodTransactions
            .GroupBy(_ => 1)
            .Select(group => new TransactionSummaryRow
            {
                CashIn = group
                    .Where(transaction =>
                        transaction.Type == AccountTransactionType.Opening ||
                        transaction.Type == AccountTransactionType.Income ||
                        transaction.Type == AccountTransactionType.TransferIn ||
                        transaction.Type == AccountTransactionType.Collection)
                    .Sum(transaction => transaction.Amount < 0 ? -transaction.Amount : transaction.Amount),
                CashOut = group
                    .Where(transaction =>
                        transaction.Type == AccountTransactionType.Expense ||
                        transaction.Type == AccountTransactionType.TransferOut ||
                        transaction.Type == AccountTransactionType.Payment)
                    .Sum(transaction => transaction.Amount < 0 ? -transaction.Amount : transaction.Amount),
                OtherSigned = group
                    .Where(transaction =>
                        transaction.Type != AccountTransactionType.Opening &&
                        transaction.Type != AccountTransactionType.Income &&
                        transaction.Type != AccountTransactionType.TransferIn &&
                        transaction.Type != AccountTransactionType.Collection &&
                        transaction.Type != AccountTransactionType.Expense &&
                        transaction.Type != AccountTransactionType.TransferOut &&
                        transaction.Type != AccountTransactionType.Payment)
                    .Sum(transaction => transaction.Amount),
                Income = group
                    .Where(transaction => transaction.Type == AccountTransactionType.Income)
                    .Sum(transaction => transaction.Amount < 0 ? -transaction.Amount : transaction.Amount),
                Expense = group
                    .Where(transaction => transaction.Type == AccountTransactionType.Expense)
                    .Sum(transaction => transaction.Amount < 0 ? -transaction.Amount : transaction.Amount)
            })) ?? new TransactionSummaryRow();

        var partnerSummary = await AsyncExecuter.FirstOrDefaultAsync(partners
            .GroupBy(_ => 1)
            .Select(group => new PartnerSummaryRow
            {
                Receivables = group.Where(partner => partner.Balance > 0).Sum(partner => partner.Balance),
                Payables = group.Where(partner => partner.Balance < 0).Sum(partner => -partner.Balance)
            })) ?? new PartnerSummaryRow();

        var productSummary = await AsyncExecuter.FirstOrDefaultAsync(products
            .GroupBy(_ => 1)
            .Select(group => new ProductSummaryRow
            {
                InventoryCostValue = group.Sum(product => product.Quantity *
                    (product.AverageInventoryCostTry ?? product.LastPurchaseCostTryAmount ??
                     product.PurchasePrice *
                     (product.PurchaseCurrency == "TRY"
                         ? 1m
                         : product.ExchangeRateAtPurchase > 0 ? product.ExchangeRateAtPurchase : 1m))),
                CriticalStockCount = group.Count(product =>
                    product.TrackStock && product.Quantity <= product.CriticalStockLevel)
            })) ?? new ProductSummaryRow();

        var flowGroups = periodTransactions
            .GroupBy(transaction => transaction.AccountId)
            .Select(group => new AccountFlowRow
            {
                AccountId = group.Key,
                Inflow = group
                    .Where(transaction =>
                        transaction.Type == AccountTransactionType.Opening ||
                        transaction.Type == AccountTransactionType.Income ||
                        transaction.Type == AccountTransactionType.TransferIn ||
                        transaction.Type == AccountTransactionType.Collection)
                    .Sum(transaction => transaction.Amount < 0 ? -transaction.Amount : transaction.Amount),
                Outflow = group
                    .Where(transaction =>
                        transaction.Type == AccountTransactionType.Expense ||
                        transaction.Type == AccountTransactionType.TransferOut ||
                        transaction.Type == AccountTransactionType.Payment)
                    .Sum(transaction => transaction.Amount < 0 ? -transaction.Amount : transaction.Amount),
                OtherSigned = group
                    .Where(transaction =>
                        transaction.Type != AccountTransactionType.Opening &&
                        transaction.Type != AccountTransactionType.Income &&
                        transaction.Type != AccountTransactionType.TransferIn &&
                        transaction.Type != AccountTransactionType.Collection &&
                        transaction.Type != AccountTransactionType.Expense &&
                        transaction.Type != AccountTransactionType.TransferOut &&
                        transaction.Type != AccountTransactionType.Payment)
                    .Sum(transaction => transaction.Amount)
            });

        var accountRows = await AsyncExecuter.ToListAsync((
            from account in accounts
            join flow in flowGroups on account.Id equals flow.AccountId into accountFlows
            from flow in accountFlows.DefaultIfEmpty()
            orderby account.Name, account.Id
            select new AccountReportItemDto
            {
                AccountId = account.Id,
                Name = account.Name,
                Type = account.Type,
                Currency = account.Currency,
                Balance = account.Balance,
                Inflow = flow.Inflow ?? 0m,
                Outflow = flow.Outflow ?? 0m,
                NetFlow = (flow.Inflow ?? 0m) - (flow.Outflow ?? 0m) + (flow.OtherSigned ?? 0m)
            })
            .Skip((accountsPage - 1) * pageSize)
            .Take(pageSize + 1));
        var accountPaging = FinalizePage(accountRows, accountsPage, pageSize);

        var partnerDocumentGroups = completedDocuments
            .Where(document =>
                document.Type == DocumentType.SaleInvoice ||
                document.Type == DocumentType.PosSale ||
                document.Type == DocumentType.PurchaseInvoice)
            .GroupBy(document => document.BusinessPartnerId)
            .Select(group => new PartnerDocumentRow
            {
                BusinessPartnerId = group.Key,
                Sales = group
                    .Where(document => document.Type == DocumentType.SaleInvoice || document.Type == DocumentType.PosSale)
                    .Sum(document => document.GrandTotal *
                        (document.Currency == "TRY" || document.ExchangeRate <= 0 ? 1m : document.ExchangeRate)),
                Purchases = group
                    .Where(document => document.Type == DocumentType.PurchaseInvoice)
                    .Sum(document => document.GrandTotal *
                        (document.Currency == "TRY" || document.ExchangeRate <= 0 ? 1m : document.ExchangeRate))
            });

        var partnerRowsQuery =
            from partner in partners
            join documentTotals in partnerDocumentGroups
                on partner.Id equals documentTotals.BusinessPartnerId into partnerDocuments
            from documentTotals in partnerDocuments.DefaultIfEmpty()
            select new BusinessPartnerReportItemDto
            {
                BusinessPartnerId = partner.Id,
                Name = partner.Name,
                Type = partner.Type,
                Currency = partner.Currency,
                Balance = partner.Balance,
                Sales = documentTotals.Sales ?? 0m,
                Purchases = documentTotals.Purchases ?? 0m
            };
        // Keep the left join server-side and page the complete partner directory. Filtering
        // projected nullable aggregate members is not translated consistently by SQLite and
        // PostgreSQL providers; bounded paging still prevents an unbounded materialization.
        var partnerRows = await AsyncExecuter.ToListAsync(partnerRowsQuery
            .OrderBy(row => row.Name)
            .ThenBy(row => row.BusinessPartnerId)
            .Skip((partnersPage - 1) * pageSize)
            .Take(pageSize + 1));
        var partnerPaging = FinalizePage(partnerRows, partnersPage, pageSize);

        var vatAggregates = await AsyncExecuter.ToListAsync(
            from item in documentItems
            join document in completedDocuments on item.DocumentId equals document.Id
            where document.Type == DocumentType.SaleInvoice ||
                  document.Type == DocumentType.PosSale ||
                  document.Type == DocumentType.PurchaseInvoice
            group new { item, document } by item.VatRate into vatGroup
            orderby vatGroup.Key
            select new VatAggregateRow
            {
                VatRate = vatGroup.Key,
                SalesVat = vatGroup
                    .Where(row => row.document.Type == DocumentType.SaleInvoice || row.document.Type == DocumentType.PosSale)
                    .Sum(row =>
                        row.item.Quantity * row.item.UnitPrice *
                        (1m - row.item.DiscountRate / 100m) * row.item.VatRate / 100m *
                        (row.document.Currency == "TRY" || row.document.ExchangeRate <= 0 ? 1m : row.document.ExchangeRate)),
                PurchaseVat = vatGroup
                    .Where(row => row.document.Type == DocumentType.PurchaseInvoice)
                    .Sum(row =>
                        row.item.Quantity * row.item.UnitPrice *
                        (1m - row.item.DiscountRate / 100m) * row.item.VatRate / 100m *
                        (row.document.Currency == "TRY" || row.document.ExchangeRate <= 0 ? 1m : row.document.ExchangeRate))
            });
        var vatRows = vatAggregates.Select(row => new VatReportItemDto
        {
            VatRate = row.VatRate,
            SalesVat = row.SalesVat,
            PurchaseVat = row.PurchaseVat,
            NetVat = row.SalesVat - row.PurchaseVat
        }).ToList();

        var profitabilityAggregates =
            from item in documentItems
            join document in salesDocuments on item.DocumentId equals document.Id
            join product in products on item.ProductId equals product.Id into itemProducts
            from product in itemProducts.DefaultIfEmpty()
            group item by new
            {
                item.ProductId,
                Name = product == null ? "Silinmiş ürün" : product.Name,
                Sku = product == null ? "-" : product.Sku
            }
            into productGroup
            select new ProfitabilityAggregateRow
            {
                ProductId = productGroup.Key.ProductId,
                Name = productGroup.Key.Name,
                Sku = productGroup.Key.Sku,
                SoldQuantity = productGroup.Sum(item => item.Quantity),
                SalesAmount = productGroup.Sum(item =>
                    item.Quantity * item.UnitPrice * (1m - item.DiscountRate / 100m)),
                EstimatedCost = productGroup.Sum(item =>
                    item.Quantity * (item.CostTryAmount ?? 0m))
            };

        var grossProfitEstimate = await AsyncExecuter.SumAsync(profitabilityAggregates
            .Select(row => row.SalesAmount - row.EstimatedCost));
        var profitabilityAggregateRows = await AsyncExecuter.ToListAsync(profitabilityAggregates
            .OrderByDescending(row => row.SalesAmount - row.EstimatedCost)
            .ThenBy(row => row.Name)
            .Skip((profitabilityPage - 1) * pageSize)
            .Take(pageSize + 1));
        var profitabilityRows = profitabilityAggregateRows.Select(row => new ProductProfitabilityReportItemDto
        {
            ProductId = row.ProductId,
            Name = row.Name,
            Sku = row.Sku,
            SoldQuantity = row.SoldQuantity,
            SalesAmount = row.SalesAmount,
            EstimatedCost = row.EstimatedCost,
            GrossProfitEstimate = row.SalesAmount - row.EstimatedCost,
            GrossMarginPercent = row.SalesAmount == 0m
                ? 0m
                : Math.Round((row.SalesAmount - row.EstimatedCost) / row.SalesAmount * 100m, 2)
        }).ToList();
        var profitabilityPaging = FinalizePage(profitabilityRows, profitabilityPage, pageSize);

        var stockRows = await AsyncExecuter.ToListAsync(products
            .OrderBy(product => product.Name)
            .ThenBy(product => product.Id)
            .Skip((stocksPage - 1) * pageSize)
            .Take(pageSize + 1)
            .Select(product => new StockReportItemDto
            {
                ProductId = product.Id,
                Name = product.Name,
                Sku = product.Sku,
                Unit = product.Unit,
                Quantity = product.Quantity,
                CriticalStockLevel = product.CriticalStockLevel,
                PurchasePrice = product.AverageInventoryCostTry ?? product.LastPurchaseCostTryAmount ??
                    product.PurchasePrice *
                    (product.PurchaseCurrency == "TRY"
                        ? 1m
                        : product.ExchangeRateAtPurchase > 0 ? product.ExchangeRateAtPurchase : 1m),
                CostValue = product.Quantity *
                    (product.AverageInventoryCostTry ?? product.LastPurchaseCostTryAmount ??
                     product.PurchasePrice *
                     (product.PurchaseCurrency == "TRY"
                         ? 1m
                         : product.ExchangeRateAtPurchase > 0 ? product.ExchangeRateAtPurchase : 1m)),
                IsCritical = product.TrackStock && product.Quantity <= product.CriticalStockLevel
            }));
        var stockPaging = FinalizePage(stockRows, stocksPage, pageSize);

        var movementRows = await AsyncExecuter.ToListAsync((
            from movement in periodMovements
            join product in products on movement.ProductId equals product.Id into movementProducts
            from product in movementProducts.DefaultIfEmpty()
            orderby movement.MovementDate descending, movement.Id descending
            select new StockMovementReportItemDto
            {
                Date = movement.MovementDate,
                ProductName = product == null ? "Silinmiş ürün" : product.Name,
                Type = movement.Type,
                Quantity = movement.Quantity,
                BalanceAfter = movement.BalanceAfter,
                Description = movement.Description
            })
            .Skip((movementsPage - 1) * pageSize)
            .Take(pageSize + 1));
        var movementPaging = FinalizePage(movementRows, movementsPage, pageSize);

        var today = Clock.Now.Date;
        var agingAggregates = await AsyncExecuter.ToListAsync(documents
            .Where(document =>
                (document.Type == DocumentType.SaleInvoice || document.Type == DocumentType.PurchaseInvoice) &&
                (document.Status == DocumentStatus.Approved || document.Status == DocumentStatus.PartiallyPaid) &&
                document.GrandTotal > document.PaidAmount)
            .Select(document => new
            {
                Bucket = !document.DueDate.HasValue || document.DueDate.Value >= today
                    ? 0
                    : document.DueDate.Value >= today.AddDays(-30)
                        ? 1
                        : document.DueDate.Value >= today.AddDays(-60)
                            ? 2
                            : document.DueDate.Value >= today.AddDays(-90) ? 3 : 4,
                document.Type,
                Amount = (document.GrandTotal - document.PaidAmount) *
                    (document.Currency == "TRY" || document.ExchangeRate <= 0 ? 1m : document.ExchangeRate)
            })
            .GroupBy(row => row.Bucket)
            .Select(group => new AgingAggregateRow
            {
                Bucket = group.Key,
                Receivables = group
                    .Where(row => row.Type == DocumentType.SaleInvoice)
                    .Sum(row => row.Amount),
                Payables = group
                    .Where(row => row.Type == DocumentType.PurchaseInvoice)
                    .Sum(row => row.Amount)
            }));
        var agingLabels = new[] { "Vadesi gelmemiş", "0-30 gün", "31-60 gün", "61-90 gün", "90+ gün" };
        var agingRows = Enumerable.Range(0, agingLabels.Length)
            .Select(bucket =>
            {
                var aggregate = agingAggregates.FirstOrDefault(row => row.Bucket == bucket);
                return new AgingReportItemDto
                {
                    Bucket = agingLabels[bucket],
                    Receivables = aggregate?.Receivables ?? 0m,
                    Payables = aggregate?.Payables ?? 0m
                };
            })
            .ToList();

        return new ReportSnapshotDto
        {
            DateFrom = dateFrom,
            DateTo = dateToExclusive.AddDays(-1),
            Summary = new ReportSummaryDto
            {
                Sales = documentSummary.Sales,
                Purchases = documentSummary.Purchases,
                GrossProfitEstimate = grossProfitEstimate,
                CashIn = transactionSummary.CashIn,
                CashOut = transactionSummary.CashOut,
                NetCashFlow = transactionSummary.CashIn - transactionSummary.CashOut + transactionSummary.OtherSigned,
                Income = transactionSummary.Income,
                Expense = transactionSummary.Expense,
                Receivables = partnerSummary.Receivables,
                Payables = partnerSummary.Payables,
                InventoryCostValue = productSummary.InventoryCostValue,
                CriticalStockCount = productSummary.CriticalStockCount
            },
            Stocks = stockRows,
            StockMovements = movementRows,
            Accounts = accountRows,
            BusinessPartners = partnerRows,
            VatRates = vatRows,
            Aging = agingRows,
            ProductProfitability = profitabilityRows,
            AccountsPaging = accountPaging,
            BusinessPartnersPaging = partnerPaging,
            ProductProfitabilityPaging = profitabilityPaging,
            StocksPaging = stockPaging,
            StockMovementsPaging = movementPaging
        };
    }

    private static int NormalizePage(int pageNumber) => Math.Clamp(pageNumber, 1, 10_000);

    private static ReportPageInfoDto FinalizePage<T>(List<T> rows, int pageNumber, int pageSize)
    {
        var hasNext = rows.Count > pageSize;
        if (hasNext)
        {
            rows.RemoveRange(pageSize, rows.Count - pageSize);
        }

        return new ReportPageInfoDto
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            HasNext = hasNext
        };
    }

    private sealed class DocumentSummaryRow
    {
        public decimal Sales { get; set; }
        public decimal Purchases { get; set; }
    }

    private sealed class TransactionSummaryRow
    {
        public decimal CashIn { get; set; }
        public decimal CashOut { get; set; }
        public decimal OtherSigned { get; set; }
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
    }

    private sealed class PartnerSummaryRow
    {
        public decimal Receivables { get; set; }
        public decimal Payables { get; set; }
    }

    private sealed class ProductSummaryRow
    {
        public decimal InventoryCostValue { get; set; }
        public int CriticalStockCount { get; set; }
    }

    private sealed class AccountFlowRow
    {
        public Guid AccountId { get; set; }
        public decimal? Inflow { get; set; }
        public decimal? Outflow { get; set; }
        public decimal? OtherSigned { get; set; }
    }

    private sealed class PartnerDocumentRow
    {
        public Guid BusinessPartnerId { get; set; }
        public decimal? Sales { get; set; }
        public decimal? Purchases { get; set; }
    }

    private sealed class VatAggregateRow
    {
        public decimal VatRate { get; set; }
        public decimal SalesVat { get; set; }
        public decimal PurchaseVat { get; set; }
    }

    private sealed class ProfitabilityAggregateRow
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public decimal SoldQuantity { get; set; }
        public decimal SalesAmount { get; set; }
        public decimal EstimatedCost { get; set; }
    }

    private sealed class AgingAggregateRow
    {
        public int Bucket { get; set; }
        public decimal Receivables { get; set; }
        public decimal Payables { get; set; }
    }
}
