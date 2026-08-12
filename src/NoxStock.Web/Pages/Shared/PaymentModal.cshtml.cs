using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NoxStock.Accounts;
using NoxStock.BusinessPartners;

namespace NoxStock.Web.Pages.Shared;

public class PaymentModalModel : NoxStockPageModel
{
    private readonly IBusinessPartnerAppService _businessPartnerAppService;
    private readonly IAccountAppService _accountAppService;

    [BindProperty]
    public ProcessPaymentDto Payment { get; set; } = new();

    public string BusinessPartnerName { get; set; } = string.Empty;
    public string Currency { get; set; } = "TRY";
    public decimal AvailableAmount { get; set; }
    public List<AccountDto> Accounts { get; set; } = new();
    public decimal PartnerBalance { get; set; }

    public PaymentModalModel(IBusinessPartnerAppService businessPartnerAppService, IAccountAppService accountAppService)
    {
        _businessPartnerAppService = businessPartnerAppService;
        _accountAppService = accountAppService;
    }

    public async Task OnGetAsync(Guid businessPartnerId, bool isCollection)
    {
        var partner = await _businessPartnerAppService.GetAsync(businessPartnerId);
        BusinessPartnerName = partner.Name;
        Currency = partner.Currency;
        AvailableAmount = isCollection ? Math.Max(0m, partner.Balance) : Math.Max(0m, -partner.Balance);
        PartnerBalance = partner.Balance;

        Payment.BusinessPartnerId = businessPartnerId;
        Payment.IsCollection = isCollection;

        var accountLookup = await _accountAppService.GetListAsync(new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto { MaxResultCount = 1000 });
        Accounts = accountLookup.Items
            .Where(x => x.IsActive && string.Equals(x.Currency, partner.Currency, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _businessPartnerAppService.ProcessPaymentAsync(Payment);
        return NoContent();
    }
}
