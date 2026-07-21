using System.Threading.Tasks;
using NoxStock.CustomerSalesHistory;
using Volo.Abp.Application.Services;

namespace NoxStock.SupplierPurchaseHistory;

public class SupplierPurchaseHistoryAppService : ApplicationService, ISupplierPurchaseHistoryAppService
{
    private readonly ICustomerSalesHistoryAppService _historyAppService;

    public SupplierPurchaseHistoryAppService(ICustomerSalesHistoryAppService historyAppService)
    {
        _historyAppService = historyAppService;
    }

    public Task<CustomerSalesHistoryDto> GetSupplierPurchaseHistoryAsync(SupplierPurchaseHistoryInput input)
    {
        return _historyAppService.GetCustomerSalesHistoryAsync(new CustomerSalesHistoryInput
        {
            CustomerId = input.SupplierId,
            IncludePurchases = true,
            PeriodPreset = input.PeriodPreset,
            StartDate = input.StartDate,
            EndDate = input.EndDate,
            DocumentType = input.DocumentType,
            ProductSearch = input.ProductSearch,
            DocumentNumber = input.DocumentNumber,
            BrandId = input.BrandId,
            CategoryId = input.CategoryId,
            Currency = input.Currency,
            SkipCount = input.SkipCount,
            MaxResultCount = input.MaxResultCount
        });
    }
}
