using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace NoxStock.Documents;

public interface IDocumentWorkflowAppService : IApplicationService
{
    Task<DocumentDto> ApproveAsync(Guid id, ApproveDocumentInputDto input);
    Task<DocumentDto> RecordPaymentAsync(Guid id, RecordDocumentPaymentInputDto input);
    Task<DocumentDto> CancelAsync(Guid id);
}
