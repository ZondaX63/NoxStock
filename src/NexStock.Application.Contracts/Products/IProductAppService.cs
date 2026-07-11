using System;
using Volo.Abp.Application.Services;

namespace NexStock.Products;

public interface IProductAppService :
    ICrudAppService<
        ProductDto,
        Guid,
        ProductGetListInput,
        CreateUpdateProductDto>
{
}
