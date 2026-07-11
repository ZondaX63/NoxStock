using Volo.Abp.Application.Dtos;

namespace NexStock.Documents;

public class DocumentGetListInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public DocumentType? Type { get; set; }
    public System.Guid? BusinessPartnerId { get; set; }
    public DocumentDueFilter DueFilter { get; set; }
    public DocumentListScope Scope { get; set; }
}
