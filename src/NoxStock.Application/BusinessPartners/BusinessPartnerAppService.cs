using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using NoxStock.Permissions;
using NoxStock.Common;
using NoxStock.MultiTenancy;
using NoxStock.BusinessPartnerTransfers;
using Volo.Abp;
using Volo.Abp.Uow;

namespace NoxStock.BusinessPartners;

[Authorize(NoxStockPermissions.BusinessPartners.Default)]
public class BusinessPartnerAppService :
    CrudAppService<
        BusinessPartner,
        BusinessPartnerDto,
        Guid,
        BusinessPartnerGetListInput,
        CreateUpdateBusinessPartnerDto>,
    IBusinessPartnerAppService
{
    private readonly IRepository<BusinessPartnerTransfer, Guid> _transferRepository;

    public BusinessPartnerAppService(
        IRepository<BusinessPartner, Guid> repository,
        IRepository<BusinessPartnerTransfer, Guid> transferRepository)
        : base(repository)
    {
        _transferRepository = transferRepository;
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

    [NoxStock.Licensing.TenantLicenseWrite]
    public override Task<BusinessPartnerDto> CreateAsync(CreateUpdateBusinessPartnerDto input)
    {
        CurrentTenant.RequireTenantId();
        return base.CreateAsync(input);
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    public override Task<BusinessPartnerDto> UpdateAsync(Guid id, CreateUpdateBusinessPartnerDto input)
    {
        CurrentTenant.RequireTenantId();
        return base.UpdateAsync(id, input);
    }

    [NoxStock.Licensing.TenantLicenseWrite]
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
            var filter = SearchText.Normalize(input.Filter);
            query = query.Where(x => x.Name.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(filter) ||
                                     (x.TaxNumber != null && x.TaxNumber.ToLower().Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c").Contains(filter)));
        }

        return query;
    }

    [NoxStock.Licensing.TenantLicenseWrite]
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

    [Authorize(NoxStockPermissions.BusinessPartners.Transfer)]
    [NoxStock.Licensing.TenantLicenseWrite]
    [UnitOfWork(true)]
    public async Task<BusinessPartnerTransferDto> TransferCustomerReceivableAsync(CreateBusinessPartnerTransferDto input)
    {
        var tenantId = CurrentTenant.RequireTenantId();
        if (input.CustomerId == Guid.Empty || input.SupplierId == Guid.Empty || input.CustomerId == input.SupplierId)
            throw new BusinessException("NoxStock:BusinessPartnerTransferInvalidParties");
        if (input.Amount <= 0m)
            throw new BusinessException("NoxStock:BusinessPartnerTransferAmountRequired");

        var customer = await Repository.GetAsync(input.CustomerId);
        var supplier = await Repository.GetAsync(input.SupplierId);
        if (customer.Type is not (BusinessPartnerType.Customer or BusinessPartnerType.Both) ||
            supplier.Type is not (BusinessPartnerType.Supplier or BusinessPartnerType.Both))
            throw new BusinessException("NoxStock:BusinessPartnerTransferInvalidParties");
        if (!string.Equals(customer.Currency, supplier.Currency, StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("NoxStock:BusinessPartnerTransferCurrencyMismatch")
                .WithData("CustomerCurrency", customer.Currency)
                .WithData("SupplierCurrency", supplier.Currency);
        if (customer.Balance < input.Amount)
            throw new BusinessException("NoxStock:BusinessPartnerTransferExceedsCustomerReceivable")
                .WithData("AvailableAmount", customer.Balance);
        if (-supplier.Balance < input.Amount)
            throw new BusinessException("NoxStock:BusinessPartnerTransferExceedsSupplierPayable")
                .WithData("AvailableAmount", -supplier.Balance);

        var date = input.TransferDate == default ? Clock.Now : input.TransferDate;
        var referenceNumber = await CreateTransferReferenceAsync(date);
        customer.Balance -= input.Amount;
        supplier.Balance += input.Amount;
        await Repository.UpdateAsync(customer, autoSave: false);
        await Repository.UpdateAsync(supplier, autoSave: false);

        var transfer = new BusinessPartnerTransfer(GuidGenerator.Create(), tenantId, customer.Id, supplier.Id,
            referenceNumber, input.Amount, customer.Currency.ToUpperInvariant(), date, input.Description?.Trim());
        await _transferRepository.InsertAsync(transfer, autoSave: true);

        return new BusinessPartnerTransferDto
        {
            Id = transfer.Id, ReferenceNumber = transfer.ReferenceNumber, Amount = transfer.Amount,
            Currency = transfer.Currency, TransferDate = transfer.TransferDate,
            CustomerName = customer.Name, SupplierName = supplier.Name, Description = transfer.Description
        };
    }

    private async Task<string> CreateTransferReferenceAsync(DateTime date)
    {
        var prefix = $"VIR-{date:yyyyMMdd}";
        var count = await _transferRepository.CountAsync(x => x.ReferenceNumber.StartsWith(prefix));
        return $"{prefix}-{count + 1:00}";
    }
}
