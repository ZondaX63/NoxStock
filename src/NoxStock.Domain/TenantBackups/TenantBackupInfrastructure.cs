using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace NoxStock.TenantBackups;

public sealed record TenantBackupDataFile(string Name, long RecordCount, byte[] Content);

public sealed record TenantBackupInvariantSummary(
    long Products,
    long BusinessPartners,
    long Documents,
    long Accounts,
    decimal ProductQuantity,
    decimal AccountBalance,
    decimal PartnerBalance,
    decimal DocumentTotal);

public sealed class TenantBackupImportContext
{
    private readonly Dictionary<(Type Type, Guid SourceId), Guid> _identifiers = new();

    public Guid SourceTenantId { get; }
    public Guid TargetTenantId { get; }
    public TenantBackupRestoreMode Mode { get; }
    public bool RemapIds => Mode == TenantBackupRestoreMode.NewTenantCloneRemapIds;

    public TenantBackupImportContext(Guid sourceTenantId, Guid targetTenantId, TenantBackupRestoreMode mode)
    {
        SourceTenantId = sourceTenantId;
        TargetTenantId = targetTenantId;
        Mode = mode;
    }

    public Guid Map(Type entityType, Guid sourceId)
    {
        if (!RemapIds) return sourceId;
        var key = (entityType, sourceId);
        if (_identifiers.TryGetValue(key, out var targetId)) return targetId;
        targetId = Guid.NewGuid();
        _identifiers.Add(key, targetId);
        return targetId;
    }
}

public interface ITenantBackupSnapshotStore
{
    Task<IReadOnlyList<TenantBackupDataFile>> ExportModuleAsync(string moduleName, Guid tenantId);
    Task ImportModuleAsync(string moduleName, TenantBackupImportContext context, IReadOnlyDictionary<string, byte[]> files);
    Task<bool> IsTenantDataEmptyAsync(Guid tenantId);
    Task<string> GetSchemaVersionAsync();
    Task<TenantBackupInvariantSummary> GetInvariantSummaryAsync(Guid tenantId);
}

public interface ITenantBackupStorage
{
    Task SaveAsync(string name, Stream content);
    Task<Stream> OpenReadAsync(string name);
    Task DeleteAsync(string name);
}

public interface IBackupEncryptionService
{
    Task EncryptAsync(Stream plaintext, Stream destination, string password);
    Task DecryptAsync(Stream encrypted, Stream destination, string password);
}
