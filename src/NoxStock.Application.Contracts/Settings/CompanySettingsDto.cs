using System;
using System.ComponentModel.DataAnnotations;

namespace NoxStock.Settings;

public class CompanySettingsDto
{
    [StringLength(128)]
    public string CompanyName { get; set; } = string.Empty;

    [StringLength(128)]
    public string TaxOffice { get; set; } = string.Empty;

    [StringLength(32)]
    public string TaxNumber { get; set; } = string.Empty;

    [StringLength(512)]
    public string Address { get; set; } = string.Empty;

    [StringLength(32)]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string DefaultCurrency { get; set; } = "TRY";

    [Range(typeof(decimal), "0", "100")]
    public decimal DefaultVatRate { get; set; } = 20;

    [Required]
    [StringLength(32)]
    public string DefaultUnit { get; set; } = "Adet";

    public Guid? DefaultCashAccountId { get; set; }
    public Guid? DefaultBankAccountId { get; set; }

    [Required]
    [StringLength(16)]
    public string SaleInvoiceNumberPrefix { get; set; } = "SF";

    [Required]
    [StringLength(16)]
    public string PurchaseInvoiceNumberPrefix { get; set; } = "AF";

    [Required]
    [StringLength(16)]
    public string OfferNumberPrefix { get; set; } = "TKL";

    [Required]
    [StringLength(16)]
    public string SaleOrderNumberPrefix { get; set; } = "SS";

    [Required]
    [StringLength(16)]
    public string PurchaseOrderNumberPrefix { get; set; } = "AS";

    [Required]
    [StringLength(16)]
    public string PosSaleNumberPrefix { get; set; } = "POS";

    [Required]
    [RegularExpression("barcode|qr")]
    public string DefaultLabelFormat { get; set; } = "barcode";
}
