using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace NoxStock.Products;

public interface IProductAppService :
    ICrudAppService<
        ProductDto,
        Guid,
        ProductGetListInput,
        CreateUpdateProductDto>
{
    Task<PagedResultDto<ProductLookupDto>> GetLookupListAsync(ProductLookupInput input);
}
