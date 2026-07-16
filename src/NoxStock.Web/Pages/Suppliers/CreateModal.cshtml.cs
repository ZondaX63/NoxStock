using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NoxStock.BusinessPartners;

namespace NoxStock.Web.Pages.Suppliers;

public class CreateModalModel : NoxStockPageModel
{
    [BindProperty]
    public CreateUpdateBusinessPartnerDto Supplier { get; set; }

    private readonly IBusinessPartnerAppService _businessPartnerAppService;

    public CreateModalModel(IBusinessPartnerAppService businessPartnerAppService)
    {
        _businessPartnerAppService = businessPartnerAppService;
        Supplier = new CreateUpdateBusinessPartnerDto
        {
            Type = BusinessPartnerType.Supplier,
            Currency = "TRY"
        };
    }

    public void OnGet()
    {
        Supplier = new CreateUpdateBusinessPartnerDto
        {
            Type = BusinessPartnerType.Supplier,
            Currency = "TRY"
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Supplier.Type = BusinessPartnerType.Supplier; // Force it
        await _businessPartnerAppService.CreateAsync(Supplier);
        return NoContent();
    }
}
