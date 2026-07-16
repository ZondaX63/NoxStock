using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace NoxStock.Products;

public interface IProductPricingAppService : IApplicationService
{
    Task<ProductPriceQuoteDto> GetQuoteAsync(ProductPriceQuoteInputDto input);
}
