using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace NexStock.Sales;

public interface IPosSaleAppService : IApplicationService
{
    Task<List<PosProductLookupDto>> GetProductsAsync(PosProductLookupInputDto input);
    Task<PosCheckoutOptionsDto> GetCheckoutOptionsAsync();
    Task<PosSaleDto> CreateAsync(CreatePosSaleDto input);
    Task<List<PosSaleDto>> GetRecentAsync();
    Task<PosSaleDto> CancelAsync(Guid id);
}
