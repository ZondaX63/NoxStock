using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NoxStock.BusinessPartners;

namespace NoxStock.Web.Pages.Shared;

public class CancelBusinessPartnerPaymentModalModel : NoxStockPageModel
{
    private readonly IBusinessPartnerAppService _service;
    [BindProperty(SupportsGet = true)] public Guid TransactionId { get; set; }
    [BindProperty] public CancelBusinessPartnerPaymentDto Cancellation { get; set; } = new();
    public CancelBusinessPartnerPaymentModalModel(IBusinessPartnerAppService service) => _service = service;
    public Task OnGetAsync() => Task.CompletedTask;
    public async Task<IActionResult> OnPostAsync()
    {
        await _service.CancelPaymentAsync(TransactionId, Cancellation);
        return NoContent();
    }
}
