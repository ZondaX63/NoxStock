using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NoxStock.BusinessPartners;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Web.Pages.BusinessPartnerTransfers;

public class CreateModalModel : NoxStockPageModel
{
    private readonly IBusinessPartnerAppService _businessPartnerAppService;

    [BindProperty(SupportsGet = true)]
    public Guid CustomerId { get; set; }

    [BindProperty]
    public CreateBusinessPartnerTransferDto Transfer { get; set; } = new();

    public string CustomerName { get; private set; } = string.Empty;
    public decimal CustomerReceivable { get; private set; }
    public string Currency { get; private set; } = "TRY";
    public List<SelectListItem> Suppliers { get; private set; } = new();

    public CreateModalModel(IBusinessPartnerAppService businessPartnerAppService)
    {
        _businessPartnerAppService = businessPartnerAppService;
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        Transfer.CustomerId = CustomerId;
        Transfer.TransferDate = Clock.Now;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        TryNormalizePostedDecimal("Transfer.Amount", value => Transfer.Amount = value);
        if (!ModelState.IsValid)
        {
            CustomerId = Transfer.CustomerId;
            await LoadAsync();
            return Page();
        }

        await _businessPartnerAppService.TransferCustomerReceivableAsync(Transfer);
        return NoContent();
    }

    private async Task LoadAsync()
    {
        var customer = await _businessPartnerAppService.GetAsync(CustomerId);
        CustomerName = customer.Name;
        CustomerReceivable = customer.Balance;
        Currency = customer.Currency;

        var suppliers = await _businessPartnerAppService.GetListAsync(new BusinessPartnerGetListInput
        {
            Type = BusinessPartnerType.Supplier,
            MaxResultCount = 1000,
            Sorting = "Name"
        });
        Suppliers = suppliers.Items
            .Where(x => x.Balance < 0m && string.Equals(x.Currency, Currency, StringComparison.OrdinalIgnoreCase))
            .Select(x => new SelectListItem($"{x.Name} · Borç: {Math.Abs(x.Balance):N2} {x.Currency}", x.Id.ToString()))
            .ToList();
        Suppliers.Insert(0, new SelectListItem("Tedarikçi seçin", string.Empty));
    }
}
