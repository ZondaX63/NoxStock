using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace NexStock.Web.MultiTenancy;

public sealed class TenantPathMiddleware
{
    public const string TenantNameItem = "NexStock.TenantName";

    private readonly RequestDelegate _next;

    public TenantPathMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (TryExtractTenantPath(context.Request.Path, out var tenantName, out var tenantPrefix, out var remainingPath))
        {
            context.Items[TenantNameItem] = tenantName;
            context.Request.PathBase = context.Request.PathBase.Add(tenantPrefix);
            context.Request.Path = remainingPath;
        }

        await _next(context);
    }

    public static bool TryExtractTenantPath(
        PathString path,
        out string tenantName,
        out PathString tenantPrefix,
        out PathString remainingPath)
    {
        tenantName = string.Empty;
        tenantPrefix = PathString.Empty;
        remainingPath = path;

        var value = path.Value;
        if (string.IsNullOrEmpty(value) || !value.StartsWith("/t/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var tenantEnd = value.IndexOf('/', 3);
        tenantName = tenantEnd < 0 ? value[3..] : value[3..tenantEnd];
        if (!IsValidTenantName(tenantName))
        {
            tenantName = string.Empty;
            return false;
        }

        tenantPrefix = new PathString(value[..(tenantEnd < 0 ? value.Length : tenantEnd)]);
        remainingPath = tenantEnd < 0 ? new PathString("/") : new PathString(value[tenantEnd..]);
        return true;
    }

    private static bool IsValidTenantName(string tenantName)
    {
        if (tenantName.Length is < 2 or > 64 || !char.IsLetterOrDigit(tenantName[0]))
        {
            return false;
        }

        foreach (var character in tenantName)
        {
            if (!char.IsLetterOrDigit(character) && character != '-')
            {
                return false;
            }
        }

        return true;
    }
}
