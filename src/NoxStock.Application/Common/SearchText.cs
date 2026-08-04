using System;

namespace NoxStock.Common;

/// <summary>
/// Common search normalization for user-entered text. Database queries apply
/// the same substitutions inline so EF Core can translate them to SQL.
/// </summary>
public static class SearchText
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        return value.Trim().ToLowerInvariant()
            .Replace('ı', 'i')
            .Replace('ğ', 'g')
            .Replace('ü', 'u')
            .Replace('ş', 's')
            .Replace('ö', 'o')
            .Replace('ç', 'c');
    }

}
