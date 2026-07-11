using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace NexStock.PrintLabels;

public interface ILabelProductLookupAppService : IApplicationService
{
    Task<List<LabelProductDto>> GetListAsync();
}
