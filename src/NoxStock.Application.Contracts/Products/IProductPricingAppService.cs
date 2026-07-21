using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Services;

namespace NoxStock.Products;

public interface IProductPricingAppService : IApplicationService
{
    Task<ProductPriceQuoteDto> GetQuoteAsync(ProductPriceQuoteInputDto input);
    Task<List<ProductCostHistoryDto>> GetCostHistoryAsync(Guid productId);
}
