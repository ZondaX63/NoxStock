using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace NoxStock.Documents;

public interface ICommercialDocumentWorkflowAppService : IApplicationService
{
    Task<DocumentDto> SendOfferAsync(Guid id);
    Task<DocumentDto> ConvertOfferToOrderAsync(Guid id);
    Task<DocumentDto> ConvertOfferToInvoiceAsync(Guid id);
    Task<DocumentDto> SendOrderAsync(Guid id);
    Task<DocumentDto> ConvertOrderToInvoiceAsync(Guid id);
}
