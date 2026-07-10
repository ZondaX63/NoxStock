using Volo.Abp.Application.Dtos;

namespace NexStock.BusinessPartners;

public class BusinessPartnerGetListInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public BusinessPartnerType? Type { get; set; }
}
