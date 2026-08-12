using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NoxStock.BusinessPartners;

namespace NoxStock.Web.Pages.Sales;

public class QuickCustomerModalModel : NoxStockPageModel
{
    private readonly IBusinessPartnerAppService _businessPartnerAppService;

    [BindProperty]
    public CreateUpdateBusinessPartnerDto Customer { get; set; } = new()
    {
        Type = BusinessPartnerType.Customer,
        Currency = "TRY"
    };

    public QuickCustomerModalModel(IBusinessPartnerAppService businessPartnerAppService)
    {
        _businessPartnerAppService = businessPartnerAppService;
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
        Customer.Type = BusinessPartnerType.Customer;
        Customer.Currency = string.IsNullOrWhiteSpace(Customer.Currency) ? "TRY" : Customer.Currency;
        var created = await _businessPartnerAppService.CreateAsync(Customer);
        return new JsonResult(new { id = created.Id, name = created.Name });
    }
}
