using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NoxStock.BusinessPartners;

namespace NoxStock.Web.Pages.Shared;

public class BusinessPartnerOpeningBalanceModalModel : NoxStockPageModel
{
    private readonly IBusinessPartnerAppService _service;

    [BindProperty]
    public CreateBusinessPartnerOpeningBalanceDto Opening { get; set; } = new();
    public BusinessPartnerDto Partner { get; private set; } = new();
    public List<SelectListItem> Directions { get; private set; } = new();

    public BusinessPartnerOpeningBalanceModalModel(IBusinessPartnerAppService service) => _service = service;

    public async Task OnGetAsync(Guid businessPartnerId)
    {
        Partner = await _service.GetAsync(businessPartnerId);
        Opening.BusinessPartnerId = businessPartnerId;
        Opening.OpeningDate = Clock.Now;
        if (Partner.Type is BusinessPartnerType.Customer or BusinessPartnerType.Both)
            Directions.Add(new SelectListItem("Bizim alacağımız", ((int)BusinessPartnerOpeningDirection.ReceivableFromPartner).ToString()));
        if (Partner.Type is BusinessPartnerType.Supplier or BusinessPartnerType.Both)
            Directions.Add(new SelectListItem("Bizim borcumuz", ((int)BusinessPartnerOpeningDirection.PayableToPartner).ToString()));
        Opening.Direction = Partner.Type == BusinessPartnerType.Supplier
            ? BusinessPartnerOpeningDirection.PayableToPartner
            : BusinessPartnerOpeningDirection.ReceivableFromPartner;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _service.CreateOpeningBalanceAsync(Opening);
        return NoContent();
    }
}
