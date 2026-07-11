using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NexStock.Accounts;
using NexStock.BusinessPartners;

namespace NexStock.Web.Pages.Shared;

public class PaymentModalModel : NexStockPageModel
{
    private readonly IBusinessPartnerAppService _businessPartnerAppService;
    private readonly IAccountAppService _accountAppService;

    [BindProperty]
    public ProcessPaymentDto Payment { get; set; } = new();

    public string BusinessPartnerName { get; set; } = string.Empty;
    public List<SelectListItem> Accounts { get; set; } = new();

    public PaymentModalModel(IBusinessPartnerAppService businessPartnerAppService, IAccountAppService accountAppService)
    {
        _businessPartnerAppService = businessPartnerAppService;
        _accountAppService = accountAppService;
    }

    public async Task OnGetAsync(Guid businessPartnerId, bool isCollection)
    {
        var partner = await _businessPartnerAppService.GetAsync(businessPartnerId);
        BusinessPartnerName = partner.Name;

        Payment.BusinessPartnerId = businessPartnerId;
        Payment.IsCollection = isCollection;

        var accountLookup = await _accountAppService.GetListAsync(new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto { MaxResultCount = 1000 });
        Accounts = accountLookup.Items.Select(x => new SelectListItem(x.Name + " (" + x.Currency + ")", x.Id.ToString())).ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _businessPartnerAppService.ProcessPaymentAsync(Payment);
        return NoContent();
    }
}
