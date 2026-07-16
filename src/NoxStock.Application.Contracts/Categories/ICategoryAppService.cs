using System;
using Volo.Abp.Application.Services;

namespace NoxStock.Categories;

public interface ICategoryAppService :
    ICrudAppService<
        CategoryDto,
        Guid,
        Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto,
        CreateUpdateCategoryDto>
{
}
