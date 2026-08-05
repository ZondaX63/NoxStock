using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace NoxStock.BusinessPartners;

public interface IBusinessPartnerAppService :
    ICrudAppService<
        BusinessPartnerDto,
        Guid,
        BusinessPartnerGetListInput,
        CreateUpdateBusinessPartnerDto>
{
    System.Threading.Tasks.Task ProcessPaymentAsync(ProcessPaymentDto input);
    System.Threading.Tasks.Task<BusinessPartnerTransferDto> TransferCustomerReceivableAsync(CreateBusinessPartnerTransferDto input);
}
