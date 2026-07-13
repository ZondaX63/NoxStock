using System;
using System.Threading.Tasks;
using NexStock.MultiTenancy;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using NexStock.Permissions;

namespace NexStock.Brands;

public class BrandAppService :
    CrudAppService<
        Brand,
        BrandDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateBrandDto>,
    IBrandAppService
{
    public BrandAppService(IRepository<Brand, Guid> repository)
        : base(repository)
    {
        GetPolicyName = NexStockPermissions.Brands.Default;
        GetListPolicyName = NexStockPermissions.Brands.Default;
        CreatePolicyName = NexStockPermissions.Brands.Create;
        UpdatePolicyName = NexStockPermissions.Brands.Edit;
        DeletePolicyName = NexStockPermissions.Brands.Delete;
    }

    public override Task<BrandDto> GetAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        return base.GetAsync(id);
    }

    public override Task<PagedResultDto<BrandDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        CurrentTenant.RequireTenantId();
        return base.GetListAsync(input);
    }

    public override Task<BrandDto> CreateAsync(CreateUpdateBrandDto input)
    {
        CurrentTenant.RequireTenantId();
        return base.CreateAsync(input);
    }

    public override Task<BrandDto> UpdateAsync(Guid id, CreateUpdateBrandDto input)
    {
        CurrentTenant.RequireTenantId();
        return base.UpdateAsync(id, input);
    }

    public override Task DeleteAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        return base.DeleteAsync(id);
    }
}
