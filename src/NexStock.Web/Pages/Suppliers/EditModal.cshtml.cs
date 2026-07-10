using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NexStock.BusinessPartners;

namespace NexStock.Web.Pages.Suppliers;

public class EditModalModel : NexStockPageModel
{
    [HiddenInput]
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public CreateUpdateBusinessPartnerDto Supplier { get; set; }

    private readonly IBusinessPartnerAppService _businessPartnerAppService;

    public EditModalModel(IBusinessPartnerAppService businessPartnerAppService)
    {
        _businessPartnerAppService = businessPartnerAppService;
        Supplier = new CreateUpdateBusinessPartnerDto();
    }

    public async Task OnGetAsync()
    {
        var dto = await _businessPartnerAppService.GetAsync(Id);
        Supplier = ObjectMapper.Map<BusinessPartnerDto, CreateUpdateBusinessPartnerDto>(dto);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Supplier.Type = BusinessPartnerType.Supplier;
        await _businessPartnerAppService.UpdateAsync(Id, Supplier);
        return NoContent();
    }
}
