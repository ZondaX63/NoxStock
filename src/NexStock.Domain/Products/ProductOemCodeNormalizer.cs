using System;
using System.Linq;

namespace NexStock.Products;

public static class ProductOemCodeNormalizer
{
    public static string Normalize(string? value)
    {
        return new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
    }
}
