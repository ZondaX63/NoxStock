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
    System.Threading.Tasks.Task CancelPaymentAsync(System.Guid transactionId, CancelBusinessPartnerPaymentDto input);
    System.Threading.Tasks.Task<BusinessPartnerOpeningBalanceDto> CreateOpeningBalanceAsync(CreateBusinessPartnerOpeningBalanceDto input);
    System.Threading.Tasks.Task<BusinessPartnerOpeningBalanceDto> CancelOpeningBalanceAsync(System.Guid openingBalanceId, CancelBusinessPartnerOpeningBalanceDto input);
    System.Threading.Tasks.Task<System.Collections.Generic.List<BusinessPartnerOpeningBalanceDto>> GetOpeningBalancesAsync(System.Guid businessPartnerId);
    System.Threading.Tasks.Task<BusinessPartnerTransferDto> TransferCustomerReceivableAsync(CreateBusinessPartnerTransferDto input);
}
