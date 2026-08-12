using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.BusinessPartners;
using Shouldly;
using Xunit;

namespace NoxStock.EntityFrameworkCore.Applications;

public class BusinessPartnerBalanceSafetyTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly IBusinessPartnerAppService _businessPartnerAppService;

    public BusinessPartnerBalanceSafetyTests()
    {
        _businessPartnerAppService = ServiceProvider.GetRequiredService<IBusinessPartnerAppService>();
    }

    [Fact]
    public async Task Should_Not_Change_Balance_When_BusinessPartner_Card_Is_Updated()
    {
        var partner = await _businessPartnerAppService.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = "Bakiye koruma müşterisi",
            Type = BusinessPartnerType.Customer,
            Currency = "TRY",
            Balance = 250m
        });
        partner.Balance.ShouldBe(0m);
        await _businessPartnerAppService.CreateOpeningBalanceAsync(new CreateBusinessPartnerOpeningBalanceDto
        {
            BusinessPartnerId = partner.Id,
            Direction = BusinessPartnerOpeningDirection.ReceivableFromPartner,
            Amount = 250m
        });

        var updated = await _businessPartnerAppService.UpdateAsync(partner.Id, new CreateUpdateBusinessPartnerDto
        {
            Name = "Bakiye koruma müşterisi güncel",
            Type = BusinessPartnerType.Customer,
            Currency = "TRY",
            Balance = 0m,
            CreditLimit = 1_000m
        });

        updated.Name.ShouldBe("Bakiye koruma müşterisi güncel");
        updated.Balance.ShouldBe(250m);
    }
}
