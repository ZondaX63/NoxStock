using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.BusinessPartners;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace NoxStock.EntityFrameworkCore.Applications;

public class BusinessPartnerOpeningBalanceAppServiceTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly IBusinessPartnerAppService _service;
    private readonly IRepository<BusinessPartner, Guid> _partnerRepository;
    private readonly IRepository<BusinessPartnerOpeningBalance, Guid> _openingRepository;

    public BusinessPartnerOpeningBalanceAppServiceTests()
    {
        _service = ServiceProvider.GetRequiredService<IBusinessPartnerAppService>();
        _partnerRepository = ServiceProvider.GetRequiredService<IRepository<BusinessPartner, Guid>>();
        _openingRepository = ServiceProvider.GetRequiredService<IRepository<BusinessPartnerOpeningBalance, Guid>>();
    }

    [Fact]
    public async Task Should_Ignore_Legacy_Create_Balance_And_Create_Cancel_Opening()
    {
        var partner = await _service.CreateAsync(new CreateUpdateBusinessPartnerDto
        {
            Name = "Açılış müşterisi", Type = BusinessPartnerType.Customer, Currency = "TRY", Balance = 999m
        });
        (await _partnerRepository.GetAsync(partner.Id)).Balance.ShouldBe(0m);

        var opening = await _service.CreateOpeningBalanceAsync(new CreateBusinessPartnerOpeningBalanceDto
        {
            BusinessPartnerId = partner.Id,
            Direction = BusinessPartnerOpeningDirection.ReceivableFromPartner,
            Amount = 300m,
            OperationKey = "partner-opening-test"
        });
        await _service.CreateOpeningBalanceAsync(new CreateBusinessPartnerOpeningBalanceDto
        {
            BusinessPartnerId = partner.Id,
            Direction = BusinessPartnerOpeningDirection.ReceivableFromPartner,
            Amount = 300m,
            OperationKey = "partner-opening-test"
        });
        (await _partnerRepository.GetAsync(partner.Id)).Balance.ShouldBe(300m);

        var duplicate = await Should.ThrowAsync<BusinessException>(() => _service.CreateOpeningBalanceAsync(new CreateBusinessPartnerOpeningBalanceDto
        {
            BusinessPartnerId = partner.Id,
            Direction = BusinessPartnerOpeningDirection.ReceivableFromPartner,
            Amount = 10m
        }));
        duplicate.Code.ShouldBe("NoxStock:BusinessPartnerOpeningAlreadyExists");

        await _service.CancelOpeningBalanceAsync(opening.Id, new CancelBusinessPartnerOpeningBalanceDto { Reason = "Hatalı devir" });
        (await _partnerRepository.GetAsync(partner.Id)).Balance.ShouldBe(0m);
        (await _openingRepository.GetListAsync(x => x.BusinessPartnerId == partner.Id)).Count.ShouldBe(2);
        (await _openingRepository.GetAsync(opening.Id)).Status.ShouldBe(BusinessPartnerOpeningStatus.Cancelled);
    }
}
