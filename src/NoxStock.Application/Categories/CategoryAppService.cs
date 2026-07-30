using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NoxStock.MultiTenancy;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using NoxStock.Permissions;

namespace NoxStock.Categories;

[Authorize(NoxStockPermissions.Categories.Default)]
public class CategoryAppService :
    CrudAppService<
        Category,
        CategoryDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateCategoryDto>,
    ICategoryAppService
{
    public CategoryAppService(IRepository<Category, Guid> repository)
        : base(repository)
    {
        GetPolicyName = NoxStockPermissions.Categories.Default;
        GetListPolicyName = NoxStockPermissions.Categories.Default;
        CreatePolicyName = NoxStockPermissions.Categories.Create;
        UpdatePolicyName = NoxStockPermissions.Categories.Edit;
        DeletePolicyName = NoxStockPermissions.Categories.Delete;
    }

    public override Task<CategoryDto> GetAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        return base.GetAsync(id);
    }

    public override Task<PagedResultDto<CategoryDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        CurrentTenant.RequireTenantId();
        return base.GetListAsync(input);
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    public override async Task<CategoryDto> CreateAsync(CreateUpdateCategoryDto input)
    {
        CurrentTenant.RequireTenantId();
        await ValidateParentAsync(input.ParentId);
        return await base.CreateAsync(input);
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    public override async Task<CategoryDto> UpdateAsync(Guid id, CreateUpdateCategoryDto input)
    {
        CurrentTenant.RequireTenantId();
        if (input.ParentId == id)
        {
            throw new BusinessException("NoxStock:CategoryCannotBeItsOwnParent");
        }

        await ValidateParentAsync(input.ParentId);
        return await base.UpdateAsync(id, input);
    }

    [NoxStock.Licensing.TenantLicenseWrite]
    public override Task DeleteAsync(Guid id)
    {
        CurrentTenant.RequireTenantId();
        return base.DeleteAsync(id);
    }

    private async Task ValidateParentAsync(Guid? parentId)
    {
        if (parentId.HasValue && await Repository.FindAsync(parentId.Value) is null)
        {
            throw new BusinessException("NoxStock:InvalidParentCategoryReference");
        }
    }
}
