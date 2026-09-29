using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Auditing;
using Volo.Abp.Content;

namespace NoxStock.TenantBackups;

public class CreateTenantBackupInputDto
{
    [Required, MinLength(12), MaxLength(256), DisableAuditing]
    public string Password { get; set; } = string.Empty;
}

public class TenantBackupDto : EntityDto<Guid>
{
    public Guid? TenantId { get; set; }
    public TenantBackupStatus Status { get; set; }
    public Guid? RequestedBy { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public long? FileSize { get; set; }
    public string? ChecksumSha256 { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string? FailureCode { get; set; }
}

public class TenantBackupDownloadTokenDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}

public class ValidateTenantBackupInputDto
{
    [Required]
    [DisableAuditing]
    public IRemoteStreamContent Package { get; set; } = default!;

    [Required, DisableAuditing]
    public string Password { get; set; } = string.Empty;
}

public class TenantBackupValidationDto
{
    public bool IsValid { get; set; }
    public string FormatVersion { get; set; } = string.Empty;
    public Guid SourceTenantId { get; set; }
    public string SourceTenantName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string SchemaVersion { get; set; } = string.Empty;
    public IReadOnlyList<string> Modules { get; set; } = Array.Empty<string>();
}

public class PreviewTenantBackupRestoreInputDto : ValidateTenantBackupInputDto
{
    public TenantBackupRestoreMode RestoreMode { get; set; } = TenantBackupRestoreMode.LocalClonePreserveIds;

    [Required, StringLength(64, MinimumLength = 2)]
    public string TargetTenantName { get; set; } = string.Empty;

    [StringLength(64, MinimumLength = 2)]
    public string? TargetTenantCode { get; set; }
}

public class RestoreTenantBackupInputDto : PreviewTenantBackupRestoreInputDto
{

    [Required, EmailAddress]
    public string AdminEmailAddress { get; set; } = string.Empty;

    [Required, DisableAuditing]
    public string AdminPassword { get; set; } = string.Empty;
}

public class TenantBackupRestorePreviewDto
{
    public TenantBackupRestoreMode RestoreMode { get; set; }
    public Guid SourceTenantId { get; set; }
    public string SourceTenantName { get; set; } = string.Empty;
    public Guid? TargetTenantId { get; set; }
    public string TargetTenantName { get; set; } = string.Empty;
    public string TargetTenantCode { get; set; } = string.Empty;
    public bool IsCompatible { get; set; }
    public IReadOnlyDictionary<string, long> ModuleRecordCounts { get; set; } = new Dictionary<string, long>();
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

public class TenantBackupRestoreResultDto
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string TenantCode { get; set; } = string.Empty;
    public TenantBackupRestoreMode RestoreMode { get; set; }
    public TenantBackupInvariantSummaryDto Invariants { get; set; } = new();
}

public class StartTenantCloneInputDto
{
    public Guid SourceBackupId { get; set; }

    [Required, StringLength(64, MinimumLength = 2)]
    public string TargetTenantName { get; set; } = string.Empty;

    [Required, StringLength(64, MinimumLength = 2)]
    public string TargetTenantCode { get; set; } = string.Empty;

    [StringLength(128)]
    public string? OperationKey { get; set; }

    [Required, DisableAuditing]
    public string Password { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string AdminEmailAddress { get; set; } = string.Empty;

    [Required, DisableAuditing]
    public string AdminPassword { get; set; } = string.Empty;
}

public class TenantCloneOperationDto : EntityDto<Guid>
{
    public Guid SourceBackupId { get; set; }
    public string TargetTenantName { get; set; } = string.Empty;
    public string TargetTenantCode { get; set; } = string.Empty;
    public TenantCloneOperationStatus Status { get; set; }
    public long ProcessedRecordCount { get; set; }
    public Guid? TargetTenantId { get; set; }
    public string? FailureCode { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public class TenantBackupInvariantSummaryDto
{
    public long Products { get; set; }
    public long BusinessPartners { get; set; }
    public long Documents { get; set; }
    public long Accounts { get; set; }
    public decimal ProductQuantity { get; set; }
    public decimal AccountBalance { get; set; }
    public decimal PartnerBalance { get; set; }
    public decimal DocumentTotal { get; set; }
}

public class TenantBackupManifest
{
    public string FormatVersion { get; set; } = "1";
    public DateTime CreatedAtUtc { get; set; }
    public Guid SourceTenantId { get; set; }
    public string SourceTenantName { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
    public List<string> ExportedModules { get; set; } = new();
    public List<TenantBackupManifestFile> Files { get; set; } = new();
    public TenantBackupEncryptionMetadata Encryption { get; set; } = new();
    public TenantBackupInvariantSummaryDto Invariants { get; set; } = new();
}

public class TenantBackupManifestFile
{
    public string Name { get; set; } = string.Empty;
    public long RecordCount { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

public class TenantBackupEncryptionMetadata
{
    public string Algorithm { get; set; } = "AES-256-GCM";
    public string KeyDerivation { get; set; } = "PBKDF2-HMAC-SHA256";
    public int Iterations { get; set; } = 310000;
}
