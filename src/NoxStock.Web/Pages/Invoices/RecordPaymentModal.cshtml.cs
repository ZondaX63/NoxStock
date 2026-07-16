using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NoxStock.Accounts;
using NoxStock.Documents;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Web.Pages.Invoices;

public class RecordPaymentModalModel : NoxStockPageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid DocumentId { get; set; }

    [BindProperty]
    public RecordPaymentInput Payment { get; set; } = new();

    public DocumentDto Document { get; private set; } = new();
    public List<SelectListItem> Accounts { get; private set; } = new();

    private readonly IDocumentAppService _documentAppService;
    private readonly IDocumentWorkflowAppService _documentWorkflowAppService;
    private readonly IAccountAppService _accountAppService;

    public RecordPaymentModalModel(
        IDocumentAppService documentAppService,
        IDocumentWorkflowAppService documentWorkflowAppService,
        IAccountAppService accountAppService)
    {
        _documentAppService = documentAppService;
        _documentWorkflowAppService = documentWorkflowAppService;
        _accountAppService = accountAppService;
    }

    public async Task OnGetAsync()
    {
        await LoadDocumentAsync();
        Payment = new RecordPaymentInput
        {
            OperationKey = Guid.NewGuid().ToString("N"),
            Amount = Document.GrandTotal - Document.PaidAmount,
            PaymentDate = DateTime.Now
        };
        await LoadAccountsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadDocumentAsync();
        if (!ModelState.IsValid)
        {
            await LoadAccountsAsync();
            return Page();
        }

        await _documentWorkflowAppService.RecordPaymentAsync(DocumentId, new RecordDocumentPaymentInputDto
        {
            OperationKey = Payment.OperationKey,
            AccountId = Payment.AccountId,
            Amount = Payment.Amount,
            PaymentDate = Payment.PaymentDate,
            Description = Payment.Description
        });

        return NoContent();
    }

    private async Task LoadDocumentAsync()
    {
        Document = await _documentAppService.GetAsync(DocumentId);
    }

    private async Task LoadAccountsAsync()
    {
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

    public class RecordPaymentInput
    {
        public string? OperationKey { get; set; }
        [Required]
        public Guid AccountId { get; set; }

        [Range(0.01, 999999999)]
        public decimal Amount { get; set; }

        [Required]
        public DateTime PaymentDate { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Description { get; set; }
    }
}
