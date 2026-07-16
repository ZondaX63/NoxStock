using System;
using Volo.Abp.Application.Services;

namespace NoxStock.Products;

public interface IProductAppService :
    ICrudAppService<
        ProductDto,
        Guid,
        ProductGetListInput,
        CreateUpdateProductDto>
{
}
