using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using NoxStock.Permissions;
using NoxStock.MultiTenancy;

namespace NoxStock.BusinessPartners;

public class BusinessPartnerAppService :
    CrudAppService<
        BusinessPartner,
        BusinessPartnerDto,
        Guid,
        BusinessPartnerGetListInput,
        CreateUpdateBusinessPartnerDto>,
    IBusinessPartnerAppService
{
    public BusinessPartnerAppService(IRepository<BusinessPartner, Guid> repository)
        : base(repository)
    {
        GetPolicyName = NoxStockPermissions.BusinessPartners.Default;
        GetListPolicyName = NoxStockPermissions.BusinessPartners.Default;
        CreatePolicyName = NoxStockPermissions.BusinessPartners.Create;
        UpdatePolicyName = NoxStockPermissions.BusinessPartners.Edit;
        DeletePolicyName = NoxStockPermissions.BusinessPartners.Delete;
    }

    public override Task<BusinessPartnerDto> GetAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        return base.GetAsync(id);
    }

    public override Task<PagedResultDto<BusinessPartnerDto>> GetListAsync(BusinessPartnerGetListInput input)
    {
        CurrentTenant.RequireTenantId();
        return base.GetListAsync(input);
    }

    public override Task<BusinessPartnerDto> CreateAsync(CreateUpdateBusinessPartnerDto input)
    {
        CurrentTenant.RequireTenantId();
        return base.CreateAsync(input);
    }

    public override Task<BusinessPartnerDto> UpdateAsync(Guid id, CreateUpdateBusinessPartnerDto input)
    {
        CurrentTenant.RequireTenantId();
        return base.UpdateAsync(id, input);
    }

    public override Task DeleteAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        return base.DeleteAsync(id);
    }

    protected override async Task<IQueryable<BusinessPartner>> CreateFilteredQueryAsync(BusinessPartnerGetListInput input)
    {
        var query = await base.CreateFilteredQueryAsync(input);

        if (input.Type.HasValue)
        {
            query = query.Where(x => x.Type == input.Type.Value || x.Type == BusinessPartnerType.Both);
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(filter) || 
                                     (x.TaxNumber != null && x.TaxNumber.Contains(filter)));
        }

        return query;
    }

    public async Task ProcessPaymentAsync(ProcessPaymentDto input)
    {
        CurrentTenant.RequireTenantId();
        await CheckUpdatePolicyAsync();

        var partner = await Repository.GetAsync(input.BusinessPartnerId);
        
        var accountRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<NoxStock.Accounts.Account, Guid>>();
        var account = await accountRepository.GetAsync(input.AccountId);

        var transactionRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<NoxStock.Accounts.AccountTransaction, Guid>>();

        if (input.IsCollection)
        {
            // Müşteriden para alıyoruz (Tahsilat). Müşterinin borcu azalır, kasamızın bakiyesi artar.
            partner.Balance -= input.Amount;
            account.Balance += input.Amount;

            await transactionRepository.InsertAsync(new NoxStock.Accounts.AccountTransaction(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                account.Id,
                NoxStock.Accounts.AccountTransactionType.Collection,
                input.Amount,
                account.Currency,
                input.Date,
                null,
                input.Description ?? $"{partner.Name} - Tahsilat"
            ));
        }
        else
        {
            // Tedarikçiye para ödüyoruz (Ödeme). Tedarikçiye olan borcumuz azalır (veyahut bakiye artar/azalır duruma göre, standart olarak Balance'ı tedarikçinin alacağı gibi düşünürsek eksi), kasamız azalır.
            partner.Balance -= input.Amount; // In standard accounting: if it's a supplier, balance is usually their credit. If we pay them, their credit balance decreases.
            account.Balance -= input.Amount;

            await transactionRepository.InsertAsync(new NoxStock.Accounts.AccountTransaction(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                account.Id,
                NoxStock.Accounts.AccountTransactionType.Payment,
                input.Amount,
                account.Currency,
                input.Date,
                null,
                input.Description ?? $"{partner.Name} - Ödeme"
            ));
        }

        await accountRepository.UpdateAsync(account);
        await Repository.UpdateAsync(partner);
    }
}
