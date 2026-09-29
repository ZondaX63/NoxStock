using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoxStock.Settings;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;
using Volo.Abp.SettingManagement;

namespace NoxStock.TenantBackups;

public sealed class TenantBackupBlobStorage : ITenantBackupStorage, ITransientDependency
{
    private readonly IBlobContainer<TenantBackupBlobContainer> _container;
    public TenantBackupBlobStorage(IBlobContainer<TenantBackupBlobContainer> container) => _container = container;
    public Task SaveAsync(string name, Stream content) => _container.SaveAsync(name, content, overrideExisting: false);
    public Task<Stream> OpenReadAsync(string name) => _container.GetAsync(name);
    public Task DeleteAsync(string name) => _container.DeleteAsync(name);
}

public sealed class AesGcmBackupEncryptionService : IBackupEncryptionService, ITransientDependency
{
    private static readonly byte[] Magic = "NOXBACK1"u8.ToArray();
    private const int Iterations = 310000;
    private const int ChunkSize = 1024 * 1024;

    public async Task EncryptAsync(Stream plaintext, Stream destination, string password)
    {
        ValidatePassword(password);
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        await destination.WriteAsync(Magic);
        await destination.WriteAsync(new byte[] { 1 });
        await destination.WriteAsync(salt);
        var integer = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(integer, Iterations);
        await destination.WriteAsync(integer);

        var buffer = new byte[ChunkSize];
        var frame = 0;
        using var aes = new AesGcm(key, 16);
        try
        {
            while (true)
            {
                var length = await ReadChunkAsync(plaintext, buffer);
                BinaryPrimitives.WriteInt32LittleEndian(integer, length);
                await destination.WriteAsync(integer);
                if (length == 0) break;
                var nonce = RandomNumberGenerator.GetBytes(12);
                var tag = new byte[16];
                var cipher = new byte[length];
                aes.Encrypt(nonce, buffer.AsSpan(0, length), cipher, tag, AssociatedData(frame));
                await destination.WriteAsync(nonce);
                await destination.WriteAsync(tag);
                await destination.WriteAsync(cipher);
                frame++;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            Array.Clear(buffer);
        }
    }

    public async Task DecryptAsync(Stream encrypted, Stream destination, string password)
    {
        ValidatePassword(password);
        var magic = new byte[Magic.Length];
        await ReadExactlyAsync(encrypted, magic);
        if (!CryptographicOperations.FixedTimeEquals(magic, Magic) || encrypted.ReadByte() != 1)
            throw new BusinessException("NoxStock:InvalidBackupFormat");
        var salt = new byte[16];
        await ReadExactlyAsync(encrypted, salt);
        var integer = new byte[4];
        await ReadExactlyAsync(encrypted, integer);
        var iterations = BinaryPrimitives.ReadInt32LittleEndian(integer);
        if (iterations < 100000 || iterations > 1000000)
            throw new BusinessException("NoxStock:InvalidBackupFormat");
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
        using var aes = new AesGcm(key, 16);
        try
        {
            for (var frame = 0; ; frame++)
            {
                await ReadExactlyAsync(encrypted, integer);
                var length = BinaryPrimitives.ReadInt32LittleEndian(integer);
                if (length == 0) break;
                if (length < 0 || length > ChunkSize) throw new BusinessException("NoxStock:InvalidBackupFormat");
                var nonce = new byte[12];
                var tag = new byte[16];
                var cipher = new byte[length];
                var plain = new byte[length];
                await ReadExactlyAsync(encrypted, nonce);
                await ReadExactlyAsync(encrypted, tag);
                await ReadExactlyAsync(encrypted, cipher);
                try { aes.Decrypt(nonce, cipher, tag, plain, AssociatedData(frame)); }
                catch (AuthenticationTagMismatchException) { throw new BusinessException("NoxStock:BackupPasswordOrIntegrityInvalid"); }
                await destination.WriteAsync(plain);
                Array.Clear(plain);
            }
            if (encrypted.ReadByte() != -1) throw new BusinessException("NoxStock:InvalidBackupFormat");
        }
        catch (EndOfStreamException)
        {
            throw new BusinessException("NoxStock:InvalidBackupFormat");
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new BusinessException("NoxStock:BackupPasswordTooShort");
    }

    private static byte[] AssociatedData(int frame) => Encoding.ASCII.GetBytes($"NOXBACK1:{frame}");

    private static async Task<int> ReadChunkAsync(Stream stream, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total));
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset));
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }
}

public interface ITenantBackupContributor
{
    string ModuleName { get; }
    int Order { get; }
    Task<IReadOnlyList<TenantBackupDataFile>> ExportAsync(Guid tenantId);
    Task ImportAsync(TenantBackupImportContext context, IReadOnlyDictionary<string, byte[]> files);
}

public abstract class RelationalTenantBackupContributor : ITenantBackupContributor
{
    private readonly ITenantBackupSnapshotStore _store;
    public abstract string ModuleName { get; }
    public abstract int Order { get; }
    protected RelationalTenantBackupContributor(ITenantBackupSnapshotStore store) => _store = store;
    public Task<IReadOnlyList<TenantBackupDataFile>> ExportAsync(Guid tenantId) => _store.ExportModuleAsync(ModuleName, tenantId);
    public Task ImportAsync(TenantBackupImportContext context, IReadOnlyDictionary<string, byte[]> files) => _store.ImportModuleAsync(ModuleName, context, files);
}

public sealed class CatalogBackupContributor(ITenantBackupSnapshotStore store) : RelationalTenantBackupContributor(store), ITransientDependency { public override string ModuleName => "catalog"; public override int Order => 20; }
public sealed class PartiesBackupContributor(ITenantBackupSnapshotStore store) : RelationalTenantBackupContributor(store), ITransientDependency { public override string ModuleName => "parties-accounts"; public override int Order => 30; }
public sealed class DocumentsBackupContributor(ITenantBackupSnapshotStore store) : RelationalTenantBackupContributor(store), ITransientDependency { public override string ModuleName => "documents"; public override int Order => 40; }
public sealed class InventoryBackupContributor(ITenantBackupSnapshotStore store) : RelationalTenantBackupContributor(store), ITransientDependency { public override string ModuleName => "inventory-cost"; public override int Order => 50; }
public sealed class FinanceBackupContributor(ITenantBackupSnapshotStore store) : RelationalTenantBackupContributor(store), ITransientDependency { public override string ModuleName => "finance"; public override int Order => 60; }
public sealed class CommandsBackupContributor(ITenantBackupSnapshotStore store) : RelationalTenantBackupContributor(store), ITransientDependency { public override string ModuleName => "completed-commands"; public override int Order => 70; }

public sealed class TenantSettingsBackupContributor : ITenantBackupContributor, ITransientDependency
{
    private static readonly string[] SafeSettings =
    [
        NoxStockSettings.CompanyName, NoxStockSettings.TaxOffice, NoxStockSettings.TaxNumber,
        NoxStockSettings.Address, NoxStockSettings.Phone, NoxStockSettings.Email,
        NoxStockSettings.DefaultCurrency, NoxStockSettings.DefaultVatRate, NoxStockSettings.DefaultUnit,
        NoxStockSettings.DefaultCashAccountId, NoxStockSettings.DefaultBankAccountId,
        NoxStockSettings.SaleInvoiceNumberPrefix, NoxStockSettings.PurchaseInvoiceNumberPrefix,
        NoxStockSettings.OfferNumberPrefix, NoxStockSettings.SaleOrderNumberPrefix,
        NoxStockSettings.PurchaseOrderNumberPrefix, NoxStockSettings.PosSaleNumberPrefix,
        NoxStockSettings.DefaultLabelFormat
    ];
    private readonly ISettingProvider _provider;
    private readonly ISettingManager _manager;
    public string ModuleName => "tenant-settings";
    public int Order => 10;
    public TenantSettingsBackupContributor(ISettingProvider provider, ISettingManager manager) { _provider = provider; _manager = manager; }

    public async Task<IReadOnlyList<TenantBackupDataFile>> ExportAsync(Guid tenantId)
    {
        var builder = new StringBuilder();
        foreach (var name in SafeSettings)
        {
            var row = new TenantSettingBackupDto(name, await _provider.GetOrNullAsync(name) ?? string.Empty);
            builder.AppendLine(JsonSerializer.Serialize(row));
        }
        return [new TenantBackupDataFile("tenant-settings/settings.jsonl", SafeSettings.Length, Encoding.UTF8.GetBytes(builder.ToString()))];
    }

    public async Task ImportAsync(TenantBackupImportContext context, IReadOnlyDictionary<string, byte[]> files)
    {
        if (!files.TryGetValue("tenant-settings/settings.jsonl", out var content)) throw new BusinessException("NoxStock:BackupModuleMissing");
        foreach (var line in Encoding.UTF8.GetString(content).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var row = JsonSerializer.Deserialize<TenantSettingBackupDto>(line) ?? throw new BusinessException("NoxStock:InvalidBackupFormat");
            if (!SafeSettings.Contains(row.Name, StringComparer.Ordinal)) throw new BusinessException("NoxStock:UnsafeBackupSetting");
            var value = row.Value;
            if (context.RemapIds &&
                (row.Name == NoxStockSettings.DefaultCashAccountId || row.Name == NoxStockSettings.DefaultBankAccountId) &&
                Guid.TryParse(value, out var sourceAccountId))
            {
                value = context.Map(typeof(NoxStock.Accounts.Account), sourceAccountId).ToString();
            }
            await _manager.SetForTenantAsync(context.TargetTenantId, row.Name, value);
        }
    }

    private sealed record TenantSettingBackupDto(string Name, string Value);
}

public sealed class TenantBackupPackageService : ITransientDependency
{
    public const long MaxPackageSizeBytes = 512L * 1024L * 1024L;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly IEnumerable<ITenantBackupContributor> _contributors;
    private readonly IBackupEncryptionService _encryption;
    private readonly ITenantBackupSnapshotStore _store;

    public TenantBackupPackageService(IEnumerable<ITenantBackupContributor> contributors, IBackupEncryptionService encryption, ITenantBackupSnapshotStore store)
    { _contributors = contributors; _encryption = encryption; _store = store; }

    public async Task<MemoryStream> CreateAsync(Guid tenantId, string tenantName, string password)
    {
        await using var zipBuffer = new MemoryStream();
        var manifest = new TenantBackupManifest
        {
            CreatedAtUtc = DateTime.UtcNow,
            SourceTenantId = tenantId,
            SourceTenantName = tenantName,
            AppVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
            SchemaVersion = await _store.GetSchemaVersionAsync(),
            Invariants = Map(await _store.GetInvariantSummaryAsync(tenantId))
        };
        using (var archive = new ZipArchive(zipBuffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var contributor in _contributors.OrderBy(x => x.Order))
            {
                manifest.ExportedModules.Add(contributor.ModuleName);
                foreach (var file in await contributor.ExportAsync(tenantId))
                {
                    var entry = archive.CreateEntry(file.Name, CompressionLevel.SmallestSize);
                    await using var target = entry.Open();
                    await target.WriteAsync(file.Content);
                    manifest.Files.Add(new TenantBackupManifestFile { Name = file.Name, RecordCount = file.RecordCount, Sha256 = Hash(file.Content) });
                }
            }
            var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.SmallestSize);
            await using var manifestStream = manifestEntry.Open();
            await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions);
        }
        zipBuffer.Position = 0;
        var encrypted = new MemoryStream();
        await _encryption.EncryptAsync(zipBuffer, encrypted, password);
        encrypted.Position = 0;
        if (encrypted.Length > MaxPackageSizeBytes)
            throw new BusinessException("NoxStock:BackupPackageTooLarge");
        return encrypted;
    }

    public async Task<(TenantBackupManifest Manifest, Dictionary<string, byte[]> Files)> ReadAndValidateAsync(Stream package, string password)
    {
        if (package.CanSeek && package.Length > MaxPackageSizeBytes)
            throw new BusinessException("NoxStock:BackupPackageTooLarge");
        await using var zipBuffer = new MemoryStream();
        await _encryption.DecryptAsync(package, zipBuffer, password);
        zipBuffer.Position = 0;
        using var archive = new ZipArchive(zipBuffer, ZipArchiveMode.Read, leaveOpen: true);
        var manifestEntry = archive.GetEntry("manifest.json") ?? throw new BusinessException("NoxStock:BackupManifestMissing");
        TenantBackupManifest manifest;
        await using (var stream = manifestEntry.Open())
            manifest = await JsonSerializer.DeserializeAsync<TenantBackupManifest>(stream, JsonOptions) ?? throw new BusinessException("NoxStock:InvalidBackupManifest");
        BackupManifestValidator.ValidateShape(manifest);
        if (!string.Equals(manifest.SchemaVersion, await _store.GetSchemaVersionAsync(), StringComparison.Ordinal))
            throw new BusinessException("NoxStock:BackupSchemaIncompatible");
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var file in manifest.Files)
        {
            var entry = archive.GetEntry(file.Name) ?? throw new BusinessException("NoxStock:BackupFileMissing");
            await using var source = entry.Open();
            await using var copy = new MemoryStream();
            await source.CopyToAsync(copy);
            var bytes = copy.ToArray();
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(file.Sha256), SHA256.HashData(bytes)))
                throw new BusinessException("NoxStock:BackupFileHashInvalid");
            var count = Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries).LongLength;
            if (count != file.RecordCount) throw new BusinessException("NoxStock:BackupFileCountInvalid");
            files.Add(file.Name, bytes);
        }
        return (manifest, files);
    }

    private static string Hash(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    private static TenantBackupInvariantSummaryDto Map(TenantBackupInvariantSummary value) => new()
    { Products = value.Products, BusinessPartners = value.BusinessPartners, Documents = value.Documents, Accounts = value.Accounts, ProductQuantity = value.ProductQuantity, AccountBalance = value.AccountBalance, PartnerBalance = value.PartnerBalance, DocumentTotal = value.DocumentTotal };
}

public static class BackupManifestValidator
{
    public static void ValidateShape(TenantBackupManifest manifest)
    {
        if (manifest.FormatVersion != "1" || manifest.SourceTenantId == Guid.Empty || string.IsNullOrWhiteSpace(manifest.SourceTenantName) || manifest.Files.Count == 0)
            throw new BusinessException("NoxStock:InvalidBackupManifest");
        if (manifest.Files.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != manifest.Files.Count ||
            manifest.Files.Any(x => x.Name.Contains("..", StringComparison.Ordinal) || x.RecordCount < 0 || x.Sha256.Length != 64))
            throw new BusinessException("NoxStock:InvalidBackupManifest");
    }
}
