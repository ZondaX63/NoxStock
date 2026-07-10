using System;
using Volo.Abp.Application.Services;

namespace NexStock.Brands;

public interface IBrandAppService :
    ICrudAppService<
        BrandDto,
        Guid,
        Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto,
        CreateUpdateBrandDto>
{
}
