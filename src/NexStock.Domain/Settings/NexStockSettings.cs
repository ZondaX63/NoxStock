namespace NexStock.Settings;

public static class NexStockSettings
{
    private const string Prefix = "NexStock";

    public const string CompanyName = Prefix + ".Company.Name";
    public const string TaxOffice = Prefix + ".Company.TaxOffice";
    public const string TaxNumber = Prefix + ".Company.TaxNumber";
    public const string Address = Prefix + ".Company.Address";
    public const string Phone = Prefix + ".Company.Phone";
    public const string Email = Prefix + ".Company.Email";

    public const string DefaultCurrency = Prefix + ".Commercial.DefaultCurrency";
    public const string DefaultVatRate = Prefix + ".Commercial.DefaultVatRate";
    public const string DefaultUnit = Prefix + ".Inventory.DefaultUnit";
    public const string DefaultCashAccountId = Prefix + ".Accounts.DefaultCashAccountId";
    public const string DefaultBankAccountId = Prefix + ".Accounts.DefaultBankAccountId";

    public const string SaleInvoiceNumberPrefix = Prefix + ".Numbering.SaleInvoicePrefix";
    public const string PurchaseInvoiceNumberPrefix = Prefix + ".Numbering.PurchaseInvoicePrefix";
    public const string OfferNumberPrefix = Prefix + ".Numbering.OfferPrefix";
    public const string SaleOrderNumberPrefix = Prefix + ".Numbering.SaleOrderPrefix";
    public const string PurchaseOrderNumberPrefix = Prefix + ".Numbering.PurchaseOrderPrefix";
    public const string PosSaleNumberPrefix = Prefix + ".Numbering.PosSalePrefix";

    public const string DefaultLabelFormat = Prefix + ".Labels.DefaultFormat";
}
