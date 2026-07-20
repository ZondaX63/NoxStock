using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace NoxStock.CustomerSalesHistory;

public interface ICustomerSalesHistoryAppService : IApplicationService
{
    Task<CustomerSalesHistoryDto> GetCustomerSalesHistoryAsync(CustomerSalesHistoryInput input);
}
