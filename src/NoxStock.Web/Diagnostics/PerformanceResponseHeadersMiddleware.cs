using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace NoxStock.Web.Diagnostics;

/// <summary>
/// Adds low-cardinality request timing and permits long-lived browser caching only when a
/// static resource URL carries an ASP.NET/ABP content fingerprint.
/// </summary>
public sealed class PerformanceResponseHeadersMiddleware
{
    private static readonly string[] StaticExtensions =
    [
        ".css", ".js", ".woff", ".woff2", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".webp", ".avif"
    ];

    private readonly RequestDelegate _next;

    public PerformanceResponseHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        context.Response.OnStarting(() =>
        {
            stopwatch.Stop();
            if (!context.Response.Headers.ContainsKey("X-Correlation-Id"))
            {
                context.Response.Headers["X-Correlation-Id"] = context.TraceIdentifier;
            }

            if (!IsStaticResource(context.Request.Path))
            {
                context.Response.Headers.Append(
                    "Server-Timing",
                    $"app;dur={stopwatch.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture)}");
            }
            else if (IsFingerprinted(context.Request.Query) && context.Response.StatusCode == StatusCodes.Status200OK)
            {
                context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }

    private static bool IsStaticResource(PathString path)
    {
        var extension = Path.GetExtension(path.Value);
        return Array.Exists(StaticExtensions, candidate =>
            string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsFingerprinted(IQueryCollection query)
    {
        return HasValue(query, "v") || HasValue(query, "_v");
    }

    private static bool HasValue(IQueryCollection query, string key)
    {
        return query.TryGetValue(key, out StringValues value) && !StringValues.IsNullOrEmpty(value);
    }
}
