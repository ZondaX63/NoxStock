using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.Accounts;
using NoxStock.Sales;
using NoxStock.Settings;
using Shouldly;
using Xunit;

namespace NoxStock.EntityFrameworkCore.Applications;

public class CompanySettingsAppServiceTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly ICompanySettingsAppService _companySettingsAppService;
    private readonly IAccountAppService _accountAppService;
    private readonly IPosSaleAppService _posSaleAppService;

    public CompanySettingsAppServiceTests()
    {
        _companySettingsAppService = ServiceProvider.GetRequiredService<ICompanySettingsAppService>();
        _accountAppService = ServiceProvider.GetRequiredService<IAccountAppService>();
        _posSaleAppService = ServiceProvider.GetRequiredService<IPosSaleAppService>();
    }

    [Fact]
    public async Task Should_Save_Operational_Defaults_And_Expose_Default_Pos_Accounts()
    {
        var cashAccount = await _accountAppService.CreateAsync(new CreateUpdateAccountDto
        {
            Name = "Default Cash",
            Type = AccountType.Cash,
            Currency = "TRY",
            IsActive = true
        });
        var bankAccount = await _accountAppService.CreateAsync(new CreateUpdateAccountDto
        {
            Name = "Default Bank",
            Type = AccountType.Bank,
            Currency = "TRY",
            IsActive = true
        });

        var saved = await _companySettingsAppService.UpdateAsync(new CompanySettingsDto
        {
            CompanyName = "NoxStock Test Ltd.",
            TaxOffice = "Kadikoy",
            TaxNumber = "1234567890",
            Address = "Istanbul",
            Phone = "5550000000",
            Email = "test@noxstock.local",
            DefaultCurrency = "USD",
            DefaultVatRate = 10,
            DefaultUnit = "Koli",
            DefaultCashAccountId = cashAccount.Id,
            DefaultBankAccountId = bankAccount.Id,
            SaleInvoiceNumberPrefix = "SAT",
            PurchaseInvoiceNumberPrefix = "ALS",
            OfferNumberPrefix = "TEK",
            SaleOrderNumberPrefix = "SSP",
            PurchaseOrderNumberPrefix = "ASP",
            PosSaleNumberPrefix = "KASA",
            DefaultLabelFormat = "qr"
        });

        saved.DefaultCurrency.ShouldBe("USD");
        saved.DefaultVatRate.ShouldBe(10);
        saved.DefaultCashAccountId.ShouldBe(cashAccount.Id);
        saved.DefaultBankAccountId.ShouldBe(bankAccount.Id);
        saved.DefaultLabelFormat.ShouldBe("qr");
        saved.PosSaleNumberPrefix.ShouldBe("KASA");

        var checkoutOptions = await _posSaleAppService.GetCheckoutOptionsAsync();
        checkoutOptions.DefaultCashAccountId.ShouldBe(cashAccount.Id);
        checkoutOptions.DefaultBankAccountId.ShouldBe(bankAccount.Id);
    }
}
