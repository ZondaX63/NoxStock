using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NoxStock.BusinessPartners;

namespace NoxStock.Web.Pages.Customers;

public class EditModalModel : NoxStockPageModel
{
    [HiddenInput]
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public CreateUpdateBusinessPartnerDto Customer { get; set; }

    private readonly IBusinessPartnerAppService _businessPartnerAppService;

    public EditModalModel(IBusinessPartnerAppService businessPartnerAppService)
    {
        _businessPartnerAppService = businessPartnerAppService;
        Customer = new CreateUpdateBusinessPartnerDto();
    }

    public async Task OnGetAsync()
    {
        var dto = await _businessPartnerAppService.GetAsync(Id);
        Customer = ObjectMapper.Map<BusinessPartnerDto, CreateUpdateBusinessPartnerDto>(dto);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Customer.Type = BusinessPartnerType.Customer;
        await _businessPartnerAppService.UpdateAsync(Id, Customer);
        return NoContent();
    }
}
