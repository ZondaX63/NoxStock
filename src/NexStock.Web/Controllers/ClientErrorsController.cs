using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace NexStock.Web.Controllers;

[ApiController]
[Authorize]
[IgnoreAntiforgeryToken]
[Route("api/diagnostics/client-errors")]
public sealed class ClientErrorsController : ControllerBase
{
    private const int MaxFieldLength = 2_000;

    private readonly ILogger<ClientErrorsController> _logger;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public ClientErrorsController(
        ILogger<ClientErrorsController> logger,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _logger = logger;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    [HttpPost]
    public IActionResult Report([FromBody] ClientErrorInput input)
    {
        _logger.LogError(
            "Client-side failure. TraceId: {TraceId}; TenantId: {TenantId}; UserId: {UserId}; Source: {Source}; Url: {Url}; Message: {Message}; Stack: {Stack}",
            HttpContext.TraceIdentifier,
            _currentTenant.Id,
            _currentUser.Id,
            Trim(input.Source),
            Trim(input.Url),
            Trim(input.Message),
            Trim(input.Stack));

        return Accepted(new { traceId = HttpContext.TraceIdentifier });
    }

    private static string? Trim(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= MaxFieldLength ? value : value[..MaxFieldLength];
    }

    public sealed class ClientErrorInput
    {
        public string? Message { get; set; }

        public string? Source { get; set; }

        public string? Url { get; set; }

        public string? Stack { get; set; }
    }
}
