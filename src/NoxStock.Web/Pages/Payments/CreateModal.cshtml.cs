using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NoxStock.Accounts;
using NoxStock.BusinessPartners;
using NoxStock.Documents;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Web.Pages.Payments;

public class CreateModalModel : NoxStockPageModel
{
    [BindProperty(SupportsGet = true)]
    public DocumentType Type { get; set; } = DocumentType.Collection;

    [BindProperty(SupportsGet = true)]
    public Guid? BusinessPartnerId { get; set; }

    [BindProperty]
    public PaymentInput Payment { get; set; } = new();

    public List<SelectListItem> BusinessPartners { get; set; } = new();
    public List<SelectListItem> Accounts { get; set; } = new();

    private readonly IBusinessPartnerAppService _businessPartnerAppService;
    private readonly IAccountAppService _accountAppService;
    private readonly IDocumentAppService _documentAppService;
    private readonly IDocumentWorkflowAppService _documentWorkflowAppService;

    public CreateModalModel(
        IBusinessPartnerAppService businessPartnerAppService,
        IAccountAppService accountAppService,
        IDocumentAppService documentAppService,
        IDocumentWorkflowAppService documentWorkflowAppService)
    {
        _businessPartnerAppService = businessPartnerAppService;
        _accountAppService = accountAppService;
        _documentAppService = documentAppService;
        _documentWorkflowAppService = documentWorkflowAppService;
    }

    public async Task OnGetAsync()
    {
        Payment = new PaymentInput
        {
            DocNumber = $"{(Type == DocumentType.Collection ? "THS" : "TED")}-{DateTime.Now:yyyyMMdd-HHmm}",
            Date = DateTime.Now,
            BusinessPartnerId = BusinessPartnerId ?? Guid.Empty,
            Amount = 0
        };

        await LoadLookupsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        TryNormalizePostedDecimal("Payment.Amount", value => Payment.Amount = value);
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync();
            return Page();
        }

        var document = await _documentAppService.CreateAsync(new CreateUpdateDocumentDto
        {
            BusinessPartnerId = Payment.BusinessPartnerId,
            DocNumber = Payment.DocNumber,
            Type = Type,
            Status = DocumentStatus.Draft, // Temporarily draft
            Currency = "TRY",
            ExchangeRate = 1,
            Date = Payment.Date,
            GrandTotal = Payment.Amount,
            Description = Payment.Description
        });

        // Approve it immediately since it's a direct payment/collection
        await _documentWorkflowAppService.ApproveAsync(document.Id, new ApproveDocumentInputDto
        {
            AccountId = Payment.AccountId,
            PaymentAmount = Payment.Amount
        });

        return NoContent();
    }

    private async Task LoadLookupsAsync()
    {
        var targetType = Type == DocumentType.Collection ? BusinessPartnerType.Customer : BusinessPartnerType.Supplier;
        var bpLookup = await _businessPartnerAppService.GetListAsync(new BusinessPartnerGetListInput
        {
            MaxResultCount = 1000,
            Type = targetType
        });
        BusinessPartners = bpLookup.Items.Select(x => new SelectListItem($"{x.Name} (Bakiye: {x.Balance} {x.Currency})", x.Id.ToString())).ToList();
        BusinessPartners.Insert(0, new SelectListItem("Cari Seçiniz...", string.Empty));

        var accountLookup = await _accountAppService.GetListAsync(new PagedAndSortedResultRequestDto
        {
            MaxResultCount = 1000
        });
        Accounts = accountLookup.Items
            .Where(x => x.IsActive)
            .Select(x => new SelectListItem($"{x.Name} - {x.Balance} {x.Currency}", x.Id.ToString()))
            .ToList();
        Accounts.Insert(0, new SelectListItem("Kasa/Banka Seçiniz...", string.Empty));
    }

    public class PaymentInput
    {
        [Required]
        [StringLength(100)]
        public string DocNumber { get; set; } = string.Empty;

        [Required]
        public DateTime Date { get; set; } = DateTime.Now;

        [Required]
        public Guid BusinessPartnerId { get; set; }

        [Required]
        public Guid AccountId { get; set; }

        [Range(0.01, 999999999)]
        public decimal Amount { get; set; }

        public string? Description { get; set; }
    }
}
