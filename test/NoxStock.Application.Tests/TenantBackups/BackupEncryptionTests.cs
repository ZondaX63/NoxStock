using System.IO;
using System.Text;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace NoxStock.TenantBackups;

public class BackupEncryptionTests
{
    private readonly AesGcmBackupEncryptionService _service = new();

    [Fact]
    public async Task Encrypted_package_should_round_trip()
    {
        await using var source = new MemoryStream(Encoding.UTF8.GetBytes("tenant-commercial-snapshot"));
        await using var encrypted = new MemoryStream();
        await _service.EncryptAsync(source, encrypted, "correct horse battery staple");
        encrypted.Position = 0;
        await using var restored = new MemoryStream();
        await _service.DecryptAsync(encrypted, restored, "correct horse battery staple");
        Encoding.UTF8.GetString(restored.ToArray()).ShouldBe("tenant-commercial-snapshot");
    }

    [Fact]
    public async Task Wrong_password_should_be_rejected()
    {
        await using var source = new MemoryStream(Encoding.UTF8.GetBytes("secret"));
        await using var encrypted = new MemoryStream();
        await _service.EncryptAsync(source, encrypted, "correct horse battery staple");
        encrypted.Position = 0;
        await Should.ThrowAsync<BusinessException>(() => _service.DecryptAsync(encrypted, new MemoryStream(), "incorrect password value"));
    }

    [Fact]
    public async Task Tampered_package_should_be_rejected()
    {
        await using var source = new MemoryStream(Encoding.UTF8.GetBytes("secret"));
        await using var encrypted = new MemoryStream();
        await _service.EncryptAsync(source, encrypted, "correct horse battery staple");
        var bytes = encrypted.ToArray();
        bytes[^1] ^= 0x40;
        await Should.ThrowAsync<BusinessException>(() => _service.DecryptAsync(new MemoryStream(bytes), new MemoryStream(), "correct horse battery staple"));
    }
}
