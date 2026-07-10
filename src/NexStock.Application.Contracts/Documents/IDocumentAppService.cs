using System;
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
}
