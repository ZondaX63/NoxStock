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
using NoxStock.Accounts;
using NoxStock.Documents;

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
    private readonly IRepository<BusinessPartnerOpeningBalance, Guid> _openingBalanceRepository;

    public BusinessPartnerAppService(
        IRepository<BusinessPartner, Guid> repository,
        IRepository<BusinessPartnerTransfer, Guid> transferRepository,
        IRepository<BusinessPartnerOpeningBalance, Guid> openingBalanceRepository)
        : base(repository)
    {
        _transferRepository = transferRepository;
        _openingBalanceRepository = openingBalanceRepository;
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

    public override async Task<PagedResultDto<BusinessPartnerDto>> GetListAsync(BusinessPartnerGetListInput input)
    {
        CurrentTenant.RequireTenantId();
        var result = await base.GetListAsync(input);
        if (result.Items.Count == 0)
            return result;

        var ids = result.Items.Select(x => x.Id).ToList();
        var documentRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<Document, Guid>>();
        var transactionRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<AccountTransaction, Guid>>();
        var documents = await documentRepository.GetListAsync(x => ids.Contains(x.BusinessPartnerId) && x.Status != DocumentStatus.Cancelled);
        var transactions = await transactionRepository.GetListAsync(x => x.BusinessPartnerId.HasValue && ids.Contains(x.BusinessPartnerId.Value));
        var today = Clock.Now.Date;
        foreach (var partner in result.Items)
        {
            var partnerDocuments = documents.Where(x => x.BusinessPartnerId == partner.Id).ToList();
            var activeTransactions = transactions.Where(x => x.BusinessPartnerId == partner.Id && x.Status == AccountTransactionStatus.Active).ToList();
            partner.OverdueAmount = partnerDocuments
                .Where(x => x.DueDate.HasValue && x.DueDate.Value.Date < today &&
                            x.Status is DocumentStatus.Approved or DocumentStatus.PartiallyPaid)
                .Sum(x => Math.Max(0m, x.GrandTotal - x.PaidAmount));
            partner.OpenDocumentCount = partnerDocuments.Count(x =>
                x.Status is DocumentStatus.Approved or DocumentStatus.PartiallyPaid && x.GrandTotal > x.PaidAmount);
            partner.LastSaleDate = partnerDocuments
                .Where(x => x.Type is DocumentType.SaleInvoice or DocumentType.PosSale)
                .Select(x => (DateTime?)x.Date).Max();
            partner.LastPurchaseDate = partnerDocuments
                .Where(x => x.Type == DocumentType.PurchaseInvoice)
                .Select(x => (DateTime?)x.Date).Max();
            partner.LastCollectionDate = activeTransactions
                .Where(x => x.Type == AccountTransactionType.Collection)
                .Select(x => (DateTime?)x.TransactionDate).Max();
            partner.LastPaymentDate = activeTransactions
                .Where(x => x.Type == AccountTransactionType.Payment)
                .Select(x => (DateTime?)x.TransactionDate).Max();
        }
        return result;
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    public override Task<BusinessPartnerDto> CreateAsync(CreateUpdateBusinessPartnerDto input)
    {
        CurrentTenant.RequireTenantId();
        // Cari kartı tanım ekranıdır. Bakiye yalnız kaynak hareketlerden oluşur.
        input.Balance = 0m;
        return base.CreateAsync(input);
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    public override async Task<BusinessPartnerDto> UpdateAsync(Guid id, CreateUpdateBusinessPartnerDto input)
    {
        CurrentTenant.RequireTenantId();
        await CheckUpdatePolicyAsync();

        // Cari kartı yalnız kimlik ve ticari varsayılanları düzenler. Bakiye,
        // tahsilat/ödeme, onaylı belge veya ilerideki açılış/uyarlama işleminden
        // değişebilir; modal/API payload'ından hiçbir zaman sessizce değişmez.
        var partner = await Repository.GetAsync(id);
        input.Balance = partner.Balance;

        return await base.UpdateAsync(id, input);
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
    [UnitOfWork(true)]
    public async Task ProcessPaymentAsync(ProcessPaymentDto input)
    {
        var tenantId = CurrentTenant.RequireTenantId();
        await AuthorizationService.CheckAsync(input.IsCollection
            ? NoxStockPermissions.BusinessPartners.Collection
            : NoxStockPermissions.BusinessPartners.Payment);

        if (input.Amount <= 0m)
            throw new BusinessException("NoxStock:BusinessPartnerPaymentAmountMustBePositive");

        var operationKey = NormalizeOperationKey(input.OperationKey);
        var transactionRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<AccountTransaction, Guid>>();
        if (await transactionRepository.AnyAsync(transaction =>
                transaction.OperationKey == operationKey &&
                transaction.BusinessPartnerId == input.BusinessPartnerId &&
                transaction.Type == (input.IsCollection ? AccountTransactionType.Collection : AccountTransactionType.Payment)))
        {
            return;
        }

        var partner = await Repository.GetAsync(input.BusinessPartnerId);
        var accountRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<NoxStock.Accounts.Account, Guid>>();
        var account = await accountRepository.GetAsync(input.AccountId);

        var isExpectedPartnerType = input.IsCollection
            ? partner.Type is BusinessPartnerType.Customer or BusinessPartnerType.Both
            : partner.Type is BusinessPartnerType.Supplier or BusinessPartnerType.Both;
        if (!isExpectedPartnerType)
            throw new BusinessException("NoxStock:BusinessPartnerPaymentTypeMismatch");
        if (!account.IsActive)
            throw new BusinessException("NoxStock:BusinessPartnerPaymentInactiveAccount");
        if (!string.Equals(partner.Currency, account.Currency, StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("NoxStock:BusinessPartnerPaymentCurrencyMismatch")
                .WithData("BusinessPartnerCurrency", partner.Currency)
                .WithData("AccountCurrency", account.Currency);
        if (input.IsCollection && partner.Balance < input.Amount)
            throw new BusinessException("NoxStock:BusinessPartnerCollectionExceedsReceivable")
                .WithData("AvailableAmount", partner.Balance);
        if (!input.IsCollection && -partner.Balance < input.Amount)
            throw new BusinessException("NoxStock:BusinessPartnerPaymentExceedsPayable")
                .WithData("AvailableAmount", -partner.Balance);
        if (!input.IsCollection && account.Balance < input.Amount)
            throw new BusinessException("NoxStock:BusinessPartnerPaymentInsufficientAccountBalance")
                .WithData("AvailableAmount", account.Balance);

        var transactionDate = input.Date == default ? Clock.Now : input.Date;
        AccountTransaction transaction;

        if (input.IsCollection)
        {
            // Müşteriden para alıyoruz (Tahsilat). Müşterinin borcu azalır, kasamızın bakiyesi artar.
            partner.Balance -= input.Amount;
            account.Balance += input.Amount;

            transaction = new AccountTransaction(
                GuidGenerator.Create(),
                tenantId,
                account.Id,
                AccountTransactionType.Collection,
                input.Amount,
                account.Currency.ToUpperInvariant(),
                transactionDate,
                null,
                input.Description ?? $"{partner.Name} - Tahsilat",
                operationKey);
        }
        else
        {
            // Tedarikçiye para ödüyoruz (Ödeme). Tedarikçiye olan borcumuz azalır (veyahut bakiye artar/azalır duruma göre, standart olarak Balance'ı tedarikçinin alacağı gibi düşünürsek eksi), kasamız azalır.
            partner.Balance += input.Amount;
            account.Balance -= input.Amount;

            transaction = new AccountTransaction(
                GuidGenerator.Create(),
                tenantId,
                account.Id,
                AccountTransactionType.Payment,
                input.Amount,
                account.Currency.ToUpperInvariant(),
                transactionDate,
                null,
                input.Description ?? $"{partner.Name} - Ödeme",
                operationKey);
        }

        transaction.BusinessPartnerId = partner.Id;
        transaction.PaymentMethod = input.PaymentMethod?.Trim();
        transaction.ReferenceNumber = input.ReferenceNumber?.Trim();
        transaction.BalanceAfter = account.Balance;
        await transactionRepository.InsertAsync(transaction);

        await accountRepository.UpdateAsync(account);
        await Repository.UpdateAsync(partner);
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    [UnitOfWork(true)]
    public async Task CancelPaymentAsync(Guid transactionId, CancelBusinessPartnerPaymentDto input)
    {
        var tenantId = CurrentTenant.RequireTenantId();
        var transactionRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<AccountTransaction, Guid>>();
        var transaction = await transactionRepository.GetAsync(transactionId);
        if (transaction.Type is not (AccountTransactionType.Collection or AccountTransactionType.Payment) ||
            transaction.DocumentId.HasValue || !transaction.BusinessPartnerId.HasValue)
            throw new BusinessException("NoxStock:BusinessPartnerPaymentMustBeCancelledFromSource");

        await AuthorizationService.CheckAsync(transaction.Type == AccountTransactionType.Collection
            ? NoxStockPermissions.BusinessPartners.CancelCollection
            : NoxStockPermissions.BusinessPartners.CancelPayment);
        if (transaction.Status == AccountTransactionStatus.Cancelled)
            return;

        var partner = await Repository.GetAsync(transaction.BusinessPartnerId.Value);
        var accountRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<Account, Guid>>();
        var account = await accountRepository.GetAsync(transaction.AccountId);
        if (transaction.Type == AccountTransactionType.Collection)
        {
            if (account.Balance < transaction.Amount)
                throw new BusinessException("NoxStock:BusinessPartnerCancellationInsufficientAccountBalance")
                    .WithData("AvailableAmount", account.Balance);
            partner.Balance += transaction.Amount;
            account.Balance -= transaction.Amount;
        }
        else
        {
            partner.Balance -= transaction.Amount;
            account.Balance += transaction.Amount;
        }

        transaction.Status = AccountTransactionStatus.Cancelled;
        transaction.CancelledAt = Clock.Now;
        transaction.CancelledBy = CurrentUser.Id;
        transaction.CancellationReason = input.Reason.Trim();
        var reversal = new AccountTransaction(
            GuidGenerator.Create(), tenantId, account.Id, AccountTransactionType.Reversal,
            transaction.Type == AccountTransactionType.Collection ? -transaction.Amount : transaction.Amount,
            transaction.Currency, Clock.Now, null,
            $"{(transaction.Type == AccountTransactionType.Collection ? "Tahsilat" : "Ödeme")} iptali: {input.Reason.Trim()}",
            $"cancel:{transaction.Id:N}")
        {
            BusinessPartnerId = partner.Id,
            ReversalOfTransactionId = transaction.Id,
            BalanceAfter = account.Balance,
            ReferenceNumber = transaction.ReferenceNumber,
            PaymentMethod = transaction.PaymentMethod
        };
        await Repository.UpdateAsync(partner);
        await accountRepository.UpdateAsync(account);
        await transactionRepository.UpdateAsync(transaction);
        await transactionRepository.InsertAsync(reversal);
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    [UnitOfWork(true)]
    public async Task<BusinessPartnerOpeningBalanceDto> CreateOpeningBalanceAsync(CreateBusinessPartnerOpeningBalanceDto input)
    {
        CurrentTenant.RequireTenantId();
        await AuthorizationService.CheckAsync(NoxStockPermissions.BusinessPartners.OpeningBalance);
        if (input.Amount <= 0m)
            throw new BusinessException("NoxStock:BusinessPartnerOpeningAmountMustBePositive");

        var operationKey = NormalizeOperationKey(input.OperationKey);
        var retry = await _openingBalanceRepository.FindAsync(x => x.OperationKey == operationKey);
        if (retry is not null)
            return MapOpening(retry);

        var partner = await Repository.GetAsync(input.BusinessPartnerId);
        ValidateOpeningDirection(partner, input.Direction);
        if (await _openingBalanceRepository.AnyAsync(x =>
                x.BusinessPartnerId == partner.Id && x.Status == BusinessPartnerOpeningStatus.Active))
            throw new BusinessException("NoxStock:BusinessPartnerOpeningAlreadyExists");

        var opening = new BusinessPartnerOpeningBalance(
            GuidGenerator.Create(), CurrentTenant.Id, partner.Id, input.Direction, input.Amount,
            partner.Currency.Trim().ToUpperInvariant(), input.OpeningDate == default ? Clock.Now : input.OpeningDate,
            operationKey, input.Description?.Trim());
        partner.Balance += OpeningEffect(opening.Direction, opening.Amount);
        await _openingBalanceRepository.InsertAsync(opening);
        await Repository.UpdateAsync(partner);
        return MapOpening(opening);
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    [UnitOfWork(true)]
    public async Task<BusinessPartnerOpeningBalanceDto> CancelOpeningBalanceAsync(Guid openingBalanceId, CancelBusinessPartnerOpeningBalanceDto input)
    {
        CurrentTenant.RequireTenantId();
        await AuthorizationService.CheckAsync(NoxStockPermissions.BusinessPartners.CancelOpeningBalance);
        var opening = await _openingBalanceRepository.GetAsync(openingBalanceId);
        if (opening.Status == BusinessPartnerOpeningStatus.Cancelled)
            return MapOpening(opening);
        if (opening.Status != BusinessPartnerOpeningStatus.Active)
            throw new BusinessException("NoxStock:BusinessPartnerOpeningCannotBeCancelled");

        var partner = await Repository.GetAsync(opening.BusinessPartnerId);
        partner.Balance -= OpeningEffect(opening.Direction, opening.Amount);
        opening.Status = BusinessPartnerOpeningStatus.Cancelled;
        opening.CancelledAt = Clock.Now;
        opening.CancelledBy = CurrentUser.Id;
        opening.CancellationReason = input.Reason.Trim();
        var reverseDirection = opening.Direction == BusinessPartnerOpeningDirection.ReceivableFromPartner
            ? BusinessPartnerOpeningDirection.PayableToPartner
            : BusinessPartnerOpeningDirection.ReceivableFromPartner;
        var reversal = new BusinessPartnerOpeningBalance(
            GuidGenerator.Create(), CurrentTenant.Id, partner.Id, reverseDirection, opening.Amount,
            opening.Currency, Clock.Now, $"cancel:{opening.Id:N}", $"Açılış bakiyesi iptali: {input.Reason.Trim()}")
        {
            Status = BusinessPartnerOpeningStatus.Reversal,
            ReversalOfOpeningBalanceId = opening.Id
        };
        await Repository.UpdateAsync(partner);
        await _openingBalanceRepository.UpdateAsync(opening);
        await _openingBalanceRepository.InsertAsync(reversal);
        return MapOpening(opening);
    }

    public async Task<System.Collections.Generic.List<BusinessPartnerOpeningBalanceDto>> GetOpeningBalancesAsync(Guid businessPartnerId)
    {
        CurrentTenant.RequireTenantId();
        var rows = await _openingBalanceRepository.GetListAsync(x => x.BusinessPartnerId == businessPartnerId);
        return rows.OrderByDescending(x => x.OpeningDate).Select(MapOpening).ToList();
    }

    [Authorize(NoxStockPermissions.BusinessPartners.Transfer)]
    [NoxStock.Licensing.TenantLicenseWrite]
    [UnitOfWork(true)]
    public Task<BusinessPartnerTransferDto> TransferCustomerReceivableAsync(CreateBusinessPartnerTransferDto input)
    {
        CurrentTenant.RequireTenantId();
        throw new BusinessException("NoxStock:BusinessPartnerTransferTemporarilyDisabled");
    }

    private static decimal OpeningEffect(BusinessPartnerOpeningDirection direction, decimal amount) =>
        direction == BusinessPartnerOpeningDirection.ReceivableFromPartner ? amount : -amount;

    private static void ValidateOpeningDirection(BusinessPartner partner, BusinessPartnerOpeningDirection direction)
    {
        if (direction == BusinessPartnerOpeningDirection.ReceivableFromPartner &&
            partner.Type is not (BusinessPartnerType.Customer or BusinessPartnerType.Both))
            throw new BusinessException("NoxStock:BusinessPartnerOpeningDirectionMismatch");
        if (direction == BusinessPartnerOpeningDirection.PayableToPartner &&
            partner.Type is not (BusinessPartnerType.Supplier or BusinessPartnerType.Both))
            throw new BusinessException("NoxStock:BusinessPartnerOpeningDirectionMismatch");
    }

    private static string NormalizeOperationKey(string? operationKey)
    {
        var normalized = string.IsNullOrWhiteSpace(operationKey) ? Guid.NewGuid().ToString("N") : operationKey.Trim();
        if (normalized.Length > 100)
            throw new BusinessException("NoxStock:OperationKeyTooLong");
        return normalized;
    }

    private static BusinessPartnerOpeningBalanceDto MapOpening(BusinessPartnerOpeningBalance row) => new()
    {
        Id = row.Id,
        BusinessPartnerId = row.BusinessPartnerId,
        Direction = row.Direction,
        Amount = row.Amount,
        Currency = row.Currency,
        OpeningDate = row.OpeningDate,
        Description = row.Description,
        Status = row.Status,
        CancelledAt = row.CancelledAt,
        CancellationReason = row.CancellationReason,
        CreationTime = row.CreationTime
    };

}
