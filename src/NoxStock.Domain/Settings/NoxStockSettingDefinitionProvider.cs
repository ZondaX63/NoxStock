using Volo.Abp.Settings;

namespace NoxStock.Settings;

public class NoxStockSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        context.Add(
            new SettingDefinition(NoxStockSettings.CompanyName, ""),
            new SettingDefinition(NoxStockSettings.TaxOffice, ""),
            new SettingDefinition(NoxStockSettings.TaxNumber, ""),
            new SettingDefinition(NoxStockSettings.Address, ""),
            new SettingDefinition(NoxStockSettings.Phone, ""),
            new SettingDefinition(NoxStockSettings.Email, ""),
            new SettingDefinition(NoxStockSettings.DefaultCurrency, "TRY"),
            new SettingDefinition(NoxStockSettings.DefaultVatRate, "20"),
            new SettingDefinition(NoxStockSettings.DefaultUnit, "Adet"),
            new SettingDefinition(NoxStockSettings.DefaultCashAccountId, ""),
            new SettingDefinition(NoxStockSettings.DefaultBankAccountId, ""),
            new SettingDefinition(NoxStockSettings.SaleInvoiceNumberPrefix, "SF"),
            new SettingDefinition(NoxStockSettings.PurchaseInvoiceNumberPrefix, "AF"),
            new SettingDefinition(NoxStockSettings.OfferNumberPrefix, "TKL"),
            new SettingDefinition(NoxStockSettings.SaleOrderNumberPrefix, "SS"),
            new SettingDefinition(NoxStockSettings.PurchaseOrderNumberPrefix, "AS"),
            new SettingDefinition(NoxStockSettings.PosSaleNumberPrefix, "POS"),
            new SettingDefinition(NoxStockSettings.DefaultLabelFormat, "barcode")
        );
    }
}
