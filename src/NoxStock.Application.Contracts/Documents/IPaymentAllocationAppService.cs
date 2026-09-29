using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace NoxStock.Documents;

public interface IPaymentAllocationAppService : IApplicationService
{
    Task<PaymentAllocationDto> CreateAsync(CreatePaymentAllocationDto input);
    Task<List<PaymentAllocationDto>> CreateBatchAsync(CreatePaymentAllocationBatchDto input);
    Task ReverseAsync(Guid id, ReversePaymentAllocationDto input);
    Task<List<PaymentAllocationDto>> GetForDocumentAsync(Guid documentId);
}
