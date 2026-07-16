using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace NoxStock.BusinessPartners;

public interface IBusinessPartnerStatementAppService : IApplicationService
{
    Task<List<BusinessPartnerPaymentHistoryDto>> GetPaymentHistoryAsync(Guid businessPartnerId);
}
