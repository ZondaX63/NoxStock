using Volo.Abp.Settings;

namespace NexStock.Settings;

public class NexStockSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        context.Add(
            new SettingDefinition(NexStockSettings.CompanyName, ""),
            new SettingDefinition(NexStockSettings.TaxOffice, ""),
            new SettingDefinition(NexStockSettings.TaxNumber, ""),
            new SettingDefinition(NexStockSettings.Address, ""),
            new SettingDefinition(NexStockSettings.Phone, ""),
            new SettingDefinition(NexStockSettings.Email, ""),
            new SettingDefinition(NexStockSettings.DefaultCurrency, "TRY"),
            new SettingDefinition(NexStockSettings.DefaultVatRate, "20"),
            new SettingDefinition(NexStockSettings.DefaultUnit, "Adet"),
            new SettingDefinition(NexStockSettings.DefaultCashAccountId, ""),
            new SettingDefinition(NexStockSettings.DefaultBankAccountId, ""),
            new SettingDefinition(NexStockSettings.SaleInvoiceNumberPrefix, "SF"),
            new SettingDefinition(NexStockSettings.PurchaseInvoiceNumberPrefix, "AF"),
            new SettingDefinition(NexStockSettings.OfferNumberPrefix, "TKL"),
            new SettingDefinition(NexStockSettings.SaleOrderNumberPrefix, "SS"),
            new SettingDefinition(NexStockSettings.PurchaseOrderNumberPrefix, "AS"),
            new SettingDefinition(NexStockSettings.PosSaleNumberPrefix, "POS"),
            new SettingDefinition(NexStockSettings.DefaultLabelFormat, "barcode")
        );
    }
}
