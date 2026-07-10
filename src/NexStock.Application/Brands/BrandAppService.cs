using System;
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
}
