using Volo.Abp.Application.Dtos;

namespace NoxStock.Accounts;

public class AccountLookupInput : PagedResultRequestDto
{
    public string? Filter { get; set; }
    public string? Currency { get; set; }
}
