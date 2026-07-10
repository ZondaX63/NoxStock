using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NexStock.BusinessPartners;

namespace NexStock.Web.Pages.Customers;

public class CreateModalModel : NexStockPageModel
{
    [BindProperty]
    public CreateUpdateBusinessPartnerDto Customer { get; set; }

    private readonly IBusinessPartnerAppService _businessPartnerAppService;

    public CreateModalModel(IBusinessPartnerAppService businessPartnerAppService)
    {
        _businessPartnerAppService = businessPartnerAppService;
        Customer = new CreateUpdateBusinessPartnerDto
        {
            Type = BusinessPartnerType.Customer,
            Currency = "TRY"
        };
    }

    public void OnGet()
    {
        Customer = new CreateUpdateBusinessPartnerDto
        {
            Type = BusinessPartnerType.Customer,
            Currency = "TRY"
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Customer.Type = BusinessPartnerType.Customer; // Force it
        await _businessPartnerAppService.CreateAsync(Customer);
        return NoContent();
    }
}
