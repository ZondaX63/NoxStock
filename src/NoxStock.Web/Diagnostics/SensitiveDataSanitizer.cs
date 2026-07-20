using System;
using System.Text.RegularExpressions;

namespace NoxStock.Web.Diagnostics;

public static partial class SensitiveDataSanitizer
{
    private const string Redacted = "[REDACTED]";

    public static string? Sanitize(string? value, int maxLength = 2_000)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var boundedValue = value.Length <= maxLength * 2 ? value : value[..(maxLength * 2)];
        var sanitized = ControlCharacters().Replace(boundedValue, " ");
        sanitized = SensitiveAssignment().Replace(sanitized, match => $"{match.Groups[1].Value}{match.Groups[2].Value}{Redacted}");
        sanitized = BearerToken().Replace(sanitized, $"Bearer {Redacted}");
        sanitized = JwtToken().Replace(sanitized, Redacted);

        return sanitized.Length <= maxLength ? sanitized : sanitized[..maxLength];
    }

    public static string? SanitizeUrl(string? value)
    {
        var sanitized = Sanitize(value);
        if (sanitized is null)
        {
            return null;
        }

        var queryIndex = sanitized.IndexOfAny(['?', '#']);
        return queryIndex < 0 ? sanitized : sanitized[..queryIndex];
    }

    [GeneratedRegex("[\\r\\n\\t]+", RegexOptions.CultureInvariant)]
    private static partial Regex ControlCharacters();

    [GeneratedRegex(
        "(?i)\\b(password|passwd|pwd|token|access[_-]?token|refresh[_-]?token|client[_-]?secret|authorization|connection[_-]?string|default[_-]?passphrase|certificate[_-]?passphrase|claim)\\b(\\s*[\\\"']?\\s*[:=]\\s*[\\\"']?)([^,;\\\"'\\s}]+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveAssignment();

    [GeneratedRegex("(?i)\\bBearer\\s+[A-Za-z0-9._~+/-]+=*", RegexOptions.CultureInvariant)]
    private static partial Regex BearerToken();

    [GeneratedRegex("\\beyJ[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+\\b", RegexOptions.CultureInvariant)]
    private static partial Regex JwtToken();
}
