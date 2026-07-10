using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using NexStock.Permissions;

namespace NexStock.BusinessPartners;

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
        GetPolicyName = NexStockPermissions.BusinessPartners.Default;
        GetListPolicyName = NexStockPermissions.BusinessPartners.Default;
        CreatePolicyName = NexStockPermissions.BusinessPartners.Create;
        UpdatePolicyName = NexStockPermissions.BusinessPartners.Edit;
        DeletePolicyName = NexStockPermissions.BusinessPartners.Delete;
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
}
