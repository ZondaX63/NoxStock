using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace NoxStock.TenantBackups;

public interface ITenantBackupAppService : IApplicationService
{
    Task<TenantBackupDto> CreateAsync(CreateTenantBackupInputDto input);
    Task<PagedResultDto<TenantBackupDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<TenantBackupDownloadTokenDto> GetDownloadTokenAsync(Guid id);
    Task<IRemoteStreamContent> DownloadAsync(Guid id, string token);
    Task DeleteAsync(Guid id);
}

public interface ITenantRestoreAppService : IApplicationService
{
    Task<TenantBackupValidationDto> ValidateAsync(ValidateTenantBackupInputDto input);
    Task<TenantBackupRestorePreviewDto> PreviewAsync(PreviewTenantBackupRestoreInputDto input);
    Task<TenantBackupRestoreResultDto> RestoreToLocalOrNewTenantAsync(RestoreTenantBackupInputDto input);
    Task<TenantCloneOperationDto> StartCloneAsync(StartTenantCloneInputDto input);
    Task<TenantCloneOperationDto> GetCloneOperationAsync(Guid id);
    Task<TenantCloneOperationDto> CancelCloneAsync(Guid id);
    Task<IReadOnlyList<TenantBackupDto>> GetCloneSourcesAsync();
}
