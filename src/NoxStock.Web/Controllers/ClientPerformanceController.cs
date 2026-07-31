using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NoxStock.Web.Diagnostics;
using Volo.Abp.Auditing;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace NoxStock.Web.Controllers;

[ApiController]
[Authorize]
[DisableAuditing]
[IgnoreAntiforgeryToken]
[Route("api/diagnostics/client-performance")]
public sealed class ClientPerformanceController : ControllerBase
{
    private readonly ILogger<ClientPerformanceController> _logger;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public ClientPerformanceController(
        ILogger<ClientPerformanceController> logger,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _logger = logger;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    [HttpPost]
    public IActionResult Report([FromBody] ClientPerformanceInput input)
    {
        _logger.LogInformation(
            "Client performance. TraceId: {TraceId}; TenantId: {TenantId}; UserId: {UserId}; Path: {Path}; Navigation: {Navigation}; TTFB: {TtfbMs}; FCP: {FcpMs}; LCP: {LcpMs}; CLS: {Cls}; Load: {LoadMs}; Requests: {RequestCount}; TransferBytes: {TransferBytes}; LongTasks: {LongTaskCount}",
            HttpContext.TraceIdentifier,
            _currentTenant.Id,
            _currentUser.Id,
            SensitiveDataSanitizer.SanitizeUrl(input.Path),
            SensitiveDataSanitizer.Sanitize(input.NavigationType, 32),
            Metric(input.TtfbMs),
            Metric(input.FcpMs),
            Metric(input.LcpMs),
            Math.Clamp(input.Cls, 0, 100),
            Metric(input.LoadMs),
            Math.Clamp(input.RequestCount, 0, 10_000),
            Math.Clamp(input.TransferBytes, 0, 1_000_000_000),
            Math.Clamp(input.LongTaskCount, 0, 10_000));

        return Accepted(new { traceId = HttpContext.TraceIdentifier });
    }

    private static double Metric(double value) => Math.Clamp(value, 0, 3_600_000);

    public sealed class ClientPerformanceInput
    {
        public string? Path { get; set; }
        public string? NavigationType { get; set; }
        public double TtfbMs { get; set; }
        public double FcpMs { get; set; }
        public double LcpMs { get; set; }
        public double Cls { get; set; }
        public double LoadMs { get; set; }
        public int RequestCount { get; set; }
        public long TransferBytes { get; set; }
        public int LongTaskCount { get; set; }
    }
}
