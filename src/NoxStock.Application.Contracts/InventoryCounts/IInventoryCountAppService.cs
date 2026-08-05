using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace NoxStock.InventoryCounts;

public interface IInventoryCountAppService : IApplicationService
{
    Task<InventoryCountDto> GetOrCreateDraftAsync();
    Task<InventoryCountDto> GetAsync(Guid id);
    Task<PagedResultDto<InventoryCountListDto>> GetListAsync(InventoryCountGetListInput input);
    Task<InventoryCountDto> SaveDraftAsync(SaveInventoryCountDto input);
    Task<InventoryCountDto> RestoreBackupAsync(RestoreInventoryCountBackupDto input);
    Task<InventoryCountDto> SubmitForApprovalAsync(Guid id);
    Task<InventoryCountDto> ReopenDraftAsync(Guid id);
    Task<InventoryCountDto> CompleteAsync(CompleteInventoryCountDto input);
    Task<QuickCreateInventoryProductResultDto> QuickCreateProductAsync(QuickCreateInventoryProductDto input);
    Task<InventoryCountImportResultDto> ImportAsync(InventoryCountImportInputDto input);
}
