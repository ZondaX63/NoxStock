using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace NoxStock.Web.Diagnostics;

public static class ProductionSecurityConfigurationValidator
{
    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (configuration.GetValue<bool>("App:RequireProductionEnvironment") && !environment.IsProduction())
        {
            throw new InvalidOperationException("This deployment requires ASPNETCORE_ENVIRONMENT=Production.");
        }

        if (!environment.IsProduction())
        {
            return;
        }

        RequireHttpsUrl(configuration["App:SelfUrl"], "App:SelfUrl");
        RequireHttpsUrl(configuration["AuthServer:Authority"], "AuthServer:Authority");

        if (!configuration.GetValue<bool>("AuthServer:RequireHttpsMetadata"))
        {
            throw new InvalidOperationException("AuthServer:RequireHttpsMetadata must be true in Production.");
        }

        if (!configuration.GetValue("App:DisablePII", true))
        {
            throw new InvalidOperationException("App:DisablePII must be true in Production.");
        }

        if (configuration.GetValue<bool>("App:EnableSwagger") ||
            configuration.GetValue<bool>("App:EnableHealthUi") ||
            configuration.GetValue<bool>("App:DetailedHealthResponse"))
        {
            throw new InvalidOperationException("Swagger, health UI and detailed health responses must be disabled in Production.");
        }

        if (!configuration.GetValue<bool>("Proxy:Enabled"))
        {
            throw new InvalidOperationException("Trusted reverse proxy forwarding must be enabled in Production.");
        }

        var knownProxies = configuration["Proxy:KnownProxies"];
        if (string.IsNullOrWhiteSpace(knownProxies))
        {
            throw new InvalidOperationException("Proxy:KnownProxies must contain at least one trusted proxy IP in Production.");
        }

        var forwardLimit = configuration.GetValue("Proxy:ForwardLimit", 1);
        if (forwardLimit is < 1 or > 2)
        {
            throw new InvalidOperationException("Proxy:ForwardLimit must be 1 or 2 in Production.");
        }

        RequireSecret(configuration.GetConnectionString("Default"), "ConnectionStrings:Default");
        RequireSecret(configuration["AuthServer:CertificatePassPhrase"], "AuthServer:CertificatePassPhrase");
        RequireSecret(configuration["StringEncryption:DefaultPassPhrase"], "StringEncryption:DefaultPassPhrase");

        var dataProtectionKeyRing = configuration["DataProtection:KeyRingPath"];
        if (string.IsNullOrWhiteSpace(dataProtectionKeyRing) || !Directory.Exists(dataProtectionKeyRing))
        {
            throw new InvalidOperationException(
                "DataProtection:KeyRingPath must reference a shared, persistent and accessible key ring in Production.");
        }

        var certificatePath = Path.Combine(environment.ContentRootPath, "openiddict.pfx");
        if (!File.Exists(certificatePath))
        {
            throw new InvalidOperationException("Production OpenIddict certificate is missing at openiddict.pfx.");
        }
    }

    private static void RequireHttpsUrl(string? value, string key)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"{key} must be an absolute HTTPS URL in Production.");
        }
    }

    private static void RequireSecret(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value) || IsPlaceholder(value))
        {
            throw new InvalidOperationException($"{key} must be supplied from a production secret source.");
        }
    }

    private static bool IsPlaceholder(string value)
    {
        return value.Contains("placeholder", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("change-me", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("dev-only", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("development-only", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("local-dev", StringComparison.OrdinalIgnoreCase);
    }
}
