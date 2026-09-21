using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace NoxStock.Documents;

public interface ICommercialCommandAppService : IApplicationService
{
    Task<List<CommercialCommandDto>> GetRecentAsync();
    Task<int> ExpireStaleAsync();
}
