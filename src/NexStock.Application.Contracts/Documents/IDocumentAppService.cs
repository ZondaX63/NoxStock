using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace NexStock.Documents;

public interface IDocumentAppService :
    ICrudAppService<
        DocumentDto,
        Guid,
        DocumentGetListInput,
        CreateUpdateDocumentDto>
{
    Task<string> GetSuggestedNumberAsync(DocumentType type);
}
