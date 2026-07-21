using System.Threading.Tasks;
using NoxStock.CustomerSalesHistory;
using Volo.Abp.Application.Services;

namespace NoxStock.SupplierPurchaseHistory;

public interface ISupplierPurchaseHistoryAppService : IApplicationService
{
    Task<CustomerSalesHistoryDto> GetSupplierPurchaseHistoryAsync(SupplierPurchaseHistoryInput input);
}
