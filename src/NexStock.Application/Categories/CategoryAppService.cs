using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using NexStock.Permissions;

namespace NexStock.Categories;

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
        GetPolicyName = NexStockPermissions.Categories.Default;
        GetListPolicyName = NexStockPermissions.Categories.Default;
        CreatePolicyName = NexStockPermissions.Categories.Create;
        UpdatePolicyName = NexStockPermissions.Categories.Edit;
        DeletePolicyName = NexStockPermissions.Categories.Delete;
    }
}
