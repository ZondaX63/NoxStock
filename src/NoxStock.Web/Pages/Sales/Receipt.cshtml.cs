using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NoxStock.Documents;
using NoxStock.Sales;

namespace NoxStock.Web.Pages.Sales;

public class ReceiptModel : NoxStockPageModel
{
    private readonly IPosSaleAppService _posSaleAppService;

    public PosReceiptDto Receipt { get; private set; } = new();

    public ReceiptModel(IPosSaleAppService posSaleAppService)
    {
        _posSaleAppService = posSaleAppService;
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            return NotFound();
        }

        Receipt = await _posSaleAppService.GetReceiptAsync(id);
        return Page();
    }

    public string PaymentMethodText() => Receipt.PaymentMethod switch
    {
        PosPaymentMethod.Cash => "Nakit",
        PosPaymentMethod.Bank => "Banka / Kart",
        PosPaymentMethod.CustomerCredit => "Cari",
        _ => "-"
    };

    public string StatusText() => Receipt.Status switch
    {
        DocumentStatus.Cancelled => "İPTAL EDİLMİŞTİR",
        DocumentStatus.Paid => "ÖDENDİ",
        DocumentStatus.Approved => "CARİYE İŞLENDİ",
        _ => Receipt.Status.ToString().ToUpperInvariant()
    };

    public static string Number(decimal value) => value.ToString("0.##");
    public static string Money(decimal value) => value.ToString("N2");
}
