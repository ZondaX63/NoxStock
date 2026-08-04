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
    Task<ProductInventorySummaryDto> GetInventorySummaryAsync();
    Task<PagedResultDto<ProductLookupDto>> GetLookupListAsync(ProductLookupInput input);
    Task<ProductImportResultDto> ImportAsync(ProductImportInputDto input);
}
