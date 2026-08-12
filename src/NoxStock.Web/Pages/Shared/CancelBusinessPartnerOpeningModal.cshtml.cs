using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NoxStock.BusinessPartners;

namespace NoxStock.Web.Pages.Shared;

public class CancelBusinessPartnerOpeningModalModel : NoxStockPageModel
{
    private readonly IBusinessPartnerAppService _service;
    [BindProperty(SupportsGet = true)] public Guid OpeningBalanceId { get; set; }
    [BindProperty] public CancelBusinessPartnerOpeningBalanceDto Cancellation { get; set; } = new();
    public BusinessPartnerOpeningBalanceDto Opening { get; private set; } = new();

    public CancelBusinessPartnerOpeningModalModel(IBusinessPartnerAppService service) => _service = service;
    public async Task OnGetAsync(Guid businessPartnerId) => Opening = (await _service.GetOpeningBalancesAsync(businessPartnerId)).Single(x => x.Id == OpeningBalanceId);
    public async Task<IActionResult> OnPostAsync()
    {
        await _service.CancelOpeningBalanceAsync(OpeningBalanceId, Cancellation);
        return NoContent();
    }
}
