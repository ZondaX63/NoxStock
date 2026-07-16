using System;
using Volo.Abp.Application.Services;

namespace NoxStock.Brands;

public interface IBrandAppService :
    ICrudAppService<
        BrandDto,
        Guid,
        Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto,
        CreateUpdateBrandDto>
{
}
