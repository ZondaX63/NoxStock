using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace NexStock.BusinessPartners;

public interface IBusinessPartnerAppService :
    ICrudAppService<
        BusinessPartnerDto,
        Guid,
        BusinessPartnerGetListInput,
        CreateUpdateBusinessPartnerDto>
{
    System.Threading.Tasks.Task ProcessPaymentAsync(ProcessPaymentDto input);
}
