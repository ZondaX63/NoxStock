using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.Brands;
using NoxStock.BusinessPartners;
using NoxStock.Categories;
using NoxStock.Documents;
using NoxStock.MultiTenancy;
using NoxStock.Permissions;
using NoxStock.Products;
using NoxStock.Common;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace NoxStock.CustomerSalesHistory;

[Authorize(NoxStockPermissions.BusinessPartners.Default)]
public class CustomerSalesHistoryAppService : ApplicationService, ICustomerSalesHistoryAppService
{
    private const int MaximumPageSize = 100;
    private const int MaximumRangeDays = 366;

    private readonly IRepository<BusinessPartner, Guid> _businessPartnerRepository;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<DocumentItem, Guid> _documentItemRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<Brand, Guid> _brandRepository;
    private readonly IRepository<Category, Guid> _categoryRepository;

    public CustomerSalesHistoryAppService(
        IRepository<BusinessPartner, Guid> businessPartnerRepository,
        IRepository<Document, Guid> documentRepository,
        IRepository<DocumentItem, Guid> documentItemRepository,
        IRepository<Product, Guid> productRepository,
        IRepository<Brand, Guid> brandRepository,
        IRepository<Category, Guid> categoryRepository)
    {
        _businessPartnerRepository = businessPartnerRepository;
        _documentRepository = documentRepository;
        _documentItemRepository = documentItemRepository;
        _productRepository = productRepository;
        _brandRepository = brandRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<CustomerSalesHistoryDto> GetCustomerSalesHistoryAsync(CustomerSalesHistoryInput input)
    {
        CurrentTenant.RequireTenantId();

        if (input.CustomerId == Guid.Empty)
        {
            throw new EntityNotFoundException(typeof(BusinessPartner), input.CustomerId);
        }

        var customer = await _businessPartnerRepository.FindAsync(input.CustomerId);
        if (customer is null || (input.IncludePurchases
                ? customer.Type is not (BusinessPartnerType.Supplier or BusinessPartnerType.Both)
                : customer.Type is not (BusinessPartnerType.Customer or BusinessPartnerType.Both)))
        {
            throw new EntityNotFoundException(typeof(BusinessPartner), input.CustomerId);
        }

        var period = ResolvePeriod(input);
        var query = await CreateRowsQueryAsync(input, period.StartDate, period.EndDate);
        var maxResultCount = Math.Clamp(input.MaxResultCount <= 0 ? 50 : input.MaxResultCount, 1, MaximumPageSize);
        var skipCount = Math.Max(input.SkipCount, 0);
        var totalCount = await AsyncExecuter.CountAsync(query);

        var pageRows = await AsyncExecuter.ToListAsync(query
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.DocumentItemId)
            .Skip(skipCount)
            .Take(maxResultCount));

        var summary = await GetSummaryAsync(query);
        var chart = await GetChartAsync(query, period.StartDate, period.EndDate);

        return new CustomerSalesHistoryDto
        {
            Customer = new CustomerSalesHistoryHeaderDto
            {
                Id = customer.Id,
                Name = customer.Name,
                CustomerCode = customer.TaxNumber,
                Phone = customer.Phone,
                Email = customer.Email,
                Balance = customer.Balance,
                CreditLimit = customer.CreditLimit,
                AvailableCredit = customer.CreditLimit > 0 ? Math.Max(customer.CreditLimit - customer.Balance, 0) : 0,
                Currency = customer.Currency
            },
            Summary = summary,
            Groups = GroupRows(pageRows, period.StartDate, period.EndDate),
            Chart = chart,
            PeriodPreset = input.PeriodPreset,
            StartDate = period.StartDate,
            EndDate = period.EndDate,
            TotalCount = totalCount,
            SkipCount = skipCount,
            MaxResultCount = maxResultCount,
            HasNextPage = skipCount + pageRows.Count < totalCount
        };
    }

    private async Task<IQueryable<CustomerSalesHistoryRowDto>> CreateRowsQueryAsync(
        CustomerSalesHistoryInput input,
        DateTime startDate,
        DateTime endDate)
    {
        var documents = await _documentRepository.GetQueryableAsync();
        var documentItems = await _documentItemRepository.GetQueryableAsync();
        var products = await _productRepository.GetQueryableAsync();
        var brands = await _brandRepository.GetQueryableAsync();
        var categories = await _categoryRepository.GetQueryableAsync();
        var endExclusive = endDate.Date.AddDays(1);

        var query =
            from item in documentItems
            join document in documents on item.DocumentId equals document.Id
            join product in products on item.ProductId equals product.Id
            join brand in brands on product.BrandId equals (Guid?)brand.Id into productBrands
            from brand in productBrands.DefaultIfEmpty()
            join category in categories on product.CategoryId equals (Guid?)category.Id into productCategories
            from category in productCategories.DefaultIfEmpty()
            where document.BusinessPartnerId == input.CustomerId
                  && (input.IncludePurchases
                      ? document.Type == DocumentType.PurchaseInvoice
                      : document.Type == DocumentType.SaleInvoice || document.Type == DocumentType.PosSale)
                  && (document.Status == DocumentStatus.Approved ||
                      document.Status == DocumentStatus.PartiallyPaid ||
                      document.Status == DocumentStatus.Paid ||
                      document.Status == DocumentStatus.Completed)
                  && document.Date >= startDate.Date
                  && document.Date < endExclusive
            select new CustomerSalesHistoryRowDto
            {
                DocumentId = document.Id,
                DocumentItemId = item.Id,
                Date = document.Date,
                TransactionType = input.IncludePurchases
                    ? CustomerSalesHistoryDocumentType.PurchaseInvoice
                    : document.Type == DocumentType.PosSale
                        ? CustomerSalesHistoryDocumentType.PosSale
                        : CustomerSalesHistoryDocumentType.SaleInvoice,
                DocumentNumber = document.DocNumber,
                ProductName = product.Name,
                Sku = product.Sku,
                Barcode = product.Barcode,
                Unit = product.Unit,
                BrandId = product.BrandId,
                BrandName = brand != null ? brand.Name : null,
                CategoryId = product.CategoryId,
                CategoryName = category != null ? category.Name : null,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                DiscountRate = item.DiscountRate,
                VatRate = item.VatRate,
                Total = item.Total,
                Currency = document.Currency
            };

        if (!input.IncludePurchases && (input.OnlyReturns || input.DocumentType == CustomerSalesHistoryDocumentType.Return))
        {
            return query.Where(_ => false);
        }

        if (input.DocumentType is CustomerSalesHistoryDocumentType.SaleInvoice or CustomerSalesHistoryDocumentType.PosSale or CustomerSalesHistoryDocumentType.PurchaseInvoice)
        {
            query = query.Where(x => x.TransactionType == input.DocumentType);
        }

        if (!string.IsNullOrWhiteSpace(input.ProductSearch))
        {
            var productSearch = SearchText.Normalize(input.ProductSearch);
            query = query.Where(x =>
                x.ProductName.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(productSearch) ||
                x.Sku.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(productSearch) ||
                (x.Barcode != null && x.Barcode.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(productSearch)));
        }

        if (!string.IsNullOrWhiteSpace(input.DocumentNumber))
        {
            var documentNumber = SearchText.Normalize(input.DocumentNumber);
            query = query.Where(x => x.DocumentNumber.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(documentNumber));
        }

        if (input.BrandId.HasValue)
        {
            var brandId = input.BrandId.Value;
            query = query.Where(x => x.BrandId == brandId);
        }

        if (input.CategoryId.HasValue)
        {
            var categoryId = input.CategoryId.Value;
            query = query.Where(x => x.CategoryId == categoryId);
        }

        if (!string.IsNullOrWhiteSpace(input.Currency))
        {
            var currency = input.Currency.Trim().ToUpperInvariant();
            query = query.Where(x => x.Currency == currency);
        }

        return query;
    }

    private async Task<CustomerSalesHistorySummaryDto> GetSummaryAsync(IQueryable<CustomerSalesHistoryRowDto> query)
    {
        var groupedAmounts = await AsyncExecuter.ToListAsync(query
            .GroupBy(x => x.Currency)
            .Select(group => new CustomerSalesHistoryCurrencyAmountDto
            {
                Currency = group.Key,
                Amount = group.Sum(x => x.Total)
            }));

        var averageAmounts = await AsyncExecuter.ToListAsync(query
            .GroupBy(x => x.Currency)
            .Select(group => new CustomerSalesHistoryCurrencyAmountDto
            {
                Currency = group.Key,
                Amount = group.Sum(x => x.Total) / group.Select(x => x.DocumentId).Distinct().Count()
            }));

        var totalQuantity = await AsyncExecuter.SumAsync(query.Select(x => x.Quantity));
        var documentCount = await AsyncExecuter.CountAsync(query.Select(x => x.DocumentId).Distinct());
        var topProduct = await AsyncExecuter.FirstOrDefaultAsync(query
            .GroupBy(x => new { x.ProductName, x.Sku })
            .Select(group => new CustomerSalesHistoryTopProductProjection
            {
                Name = group.Key.ProductName,
                Sku = group.Key.Sku,
                Quantity = group.Sum(x => x.Quantity)
            })
            .OrderByDescending(x => x.Quantity)
            .ThenBy(x => x.Name));

        return new CustomerSalesHistorySummaryDto
        {
            NetSales = groupedAmounts.OrderBy(x => x.Currency).ToList(),
            TotalQuantity = totalQuantity,
            DocumentCount = documentCount,
            AverageDocumentAmount = averageAmounts.OrderBy(x => x.Currency).ToList(),
            TopProductName = topProduct?.Name,
            TopProductSku = topProduct?.Sku,
            TopProductQuantity = topProduct?.Quantity ?? 0
        };
    }

    private async Task<List<CustomerSalesHistoryChartPointDto>> GetChartAsync(
        IQueryable<CustomerSalesHistoryRowDto> query,
        DateTime startDate,
        DateTime endDate)
    {
        var dailyTotals = await AsyncExecuter.ToListAsync(query
            .GroupBy(x => new { Day = x.Date.Date, x.Currency })
            .Select(group => new CustomerSalesHistoryDailyTotalProjection
            {
                Date = group.Key.Day,
                Currency = group.Key.Currency,
                Amount = group.Sum(x => x.Total)
            }));

        var days = (endDate.Date - startDate.Date).Days + 1;
        if (days <= 14)
        {
            return CreateChartPoints(dailyTotals, item => item.Date.Date, item => item.Date.Date);
        }

        if (days <= 93)
        {
            return CreateChartPoints(
                dailyTotals,
                item => StartOfWeek(item.Date),
                item => StartOfWeek(item.Date).AddDays(6));
        }

        return CreateChartPoints(
            dailyTotals,
            item => new DateTime(item.Date.Year, item.Date.Month, 1),
            item => new DateTime(item.Date.Year, item.Date.Month, 1).AddMonths(1).AddDays(-1));
    }

    private static List<CustomerSalesHistoryChartPointDto> CreateChartPoints(
        IEnumerable<CustomerSalesHistoryDailyTotalProjection> totals,
        Func<CustomerSalesHistoryDailyTotalProjection, DateTime> startSelector,
        Func<CustomerSalesHistoryDailyTotalProjection, DateTime> endSelector)
    {
        return totals
            .GroupBy(item => new { StartDate = startSelector(item), EndDate = endSelector(item) })
            .OrderBy(group => group.Key.StartDate)
            .Select(group => new CustomerSalesHistoryChartPointDto
            {
                StartDate = group.Key.StartDate,
                EndDate = group.Key.EndDate,
                Totals = group
                    .GroupBy(item => item.Currency)
                    .Select(currency => new CustomerSalesHistoryCurrencyAmountDto
                    {
                        Currency = currency.Key,
                        Amount = currency.Sum(item => item.Amount)
                    })
                    .OrderBy(item => item.Currency)
                    .ToList()
            })
            .ToList();
    }

    private static List<CustomerSalesHistoryGroupDto> GroupRows(
        List<CustomerSalesHistoryRowDto> rows,
        DateTime startDate,
        DateTime endDate)
    {
        var days = (endDate.Date - startDate.Date).Days + 1;
        var groups = rows.GroupBy(row => days <= 93 ? StartOfWeek(row.Date) : new DateTime(row.Date.Year, row.Date.Month, 1));

        return groups
            .OrderByDescending(group => group.Key)
            .Select(group => new CustomerSalesHistoryGroupDto
            {
                StartDate = group.Key,
                EndDate = days <= 93 ? group.Key.AddDays(6) : group.Key.AddMonths(1).AddDays(-1),
                Quantity = group.Sum(row => row.Quantity),
                TransactionCount = group.Select(row => row.DocumentId).Distinct().LongCount(),
                Totals = group
                    .GroupBy(row => row.Currency)
                    .Select(currency => new CustomerSalesHistoryCurrencyAmountDto
                    {
                        Currency = currency.Key,
                        Amount = currency.Sum(row => row.Total)
                    })
                    .OrderBy(item => item.Currency)
                    .ToList(),
                Rows = group.OrderByDescending(row => row.Date).ThenByDescending(row => row.DocumentItemId).ToList()
            })
            .ToList();
    }

    private (DateTime StartDate, DateTime EndDate) ResolvePeriod(CustomerSalesHistoryInput input)
    {
        var today = Clock.Now.Date;
        DateTime startDate;
        DateTime endDate;

        switch (input.PeriodPreset)
        {
            case CustomerSalesHistoryPeriodPreset.ThisWeek:
                startDate = StartOfWeek(today);
                endDate = startDate.AddDays(6);
                break;
            case CustomerSalesHistoryPeriodPreset.LastWeek:
                endDate = StartOfWeek(today).AddDays(-1);
                startDate = endDate.AddDays(-6);
                break;
            case CustomerSalesHistoryPeriodPreset.ThisMonth:
                startDate = new DateTime(today.Year, today.Month, 1);
                endDate = startDate.AddMonths(1).AddDays(-1);
                break;
            case CustomerSalesHistoryPeriodPreset.LastMonth:
                endDate = new DateTime(today.Year, today.Month, 1).AddDays(-1);
                startDate = new DateTime(endDate.Year, endDate.Month, 1);
                break;
            case CustomerSalesHistoryPeriodPreset.LastThreeMonths:
                startDate = new DateTime(today.Year, today.Month, 1).AddMonths(-2);
                endDate = new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);
                break;
            case CustomerSalesHistoryPeriodPreset.ThisYear:
                startDate = new DateTime(today.Year, 1, 1);
                endDate = new DateTime(today.Year, 12, 31);
                break;
            case CustomerSalesHistoryPeriodPreset.Custom:
                startDate = input.StartDate?.Date ?? today.AddDays(-30);
                endDate = input.EndDate?.Date ?? today;
                break;
            default:
                startDate = new DateTime(today.Year, today.Month, 1);
                endDate = startDate.AddMonths(1).AddDays(-1);
                break;
        }

        if (endDate < startDate)
        {
            throw new BusinessException("NoxStock:InvalidSalesHistoryDateRange");
        }

        if ((endDate - startDate).Days + 1 > MaximumRangeDays)
        {
            throw new BusinessException("NoxStock:SalesHistoryDateRangeTooLarge")
                .WithData("MaximumRangeDays", MaximumRangeDays);
        }

        return (startDate, endDate);
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-offset);
    }

    private sealed class CustomerSalesHistoryTopProductProjection
    {
        public string Name { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
    }

    private sealed class CustomerSalesHistoryDailyTotalProjection
    {
        public DateTime Date { get; set; }
        public string Currency { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
