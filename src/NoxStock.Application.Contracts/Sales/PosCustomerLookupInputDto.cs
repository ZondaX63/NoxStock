using Volo.Abp.Application.Dtos;

namespace NoxStock.Sales;

public class PosCustomerLookupInputDto : PagedResultRequestDto
{
    public string? Filter { get; set; }
}
