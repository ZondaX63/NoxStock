using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Linq;
using Volo.Abp.Security.Claims;

namespace NoxStock.Security;

public sealed class AuthenticatedTestAuthorizationService : IAuthorizationService
{
    public Task<AuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal user,
        object? resource,
        IEnumerable<IAuthorizationRequirement> requirements)
    {
        return Task.FromResult(IsAuthenticated(user) && IsAllowedForTenantBoundary(user, GetPolicies(resource))
            ? AuthorizationResult.Success()
            : AuthorizationResult.Failed());
    }

    public Task<AuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal user,
        object? resource,
        string policyName)
    {
        return Task.FromResult(IsAuthenticated(user) && IsAllowedForTenantBoundary(user, [policyName])
            ? AuthorizationResult.Success()
            : AuthorizationResult.Failed());
    }

    private static bool IsAuthenticated(ClaimsPrincipal user)
    {
        return user.Identity?.IsAuthenticated == true;
    }

    private static bool IsAllowedForTenantBoundary(ClaimsPrincipal user, IEnumerable<string> policyNames)
    {
        var isTenantUser = user.FindFirst(AbpClaimTypes.TenantId) is not null;
        if (!isTenantUser)
        {
            return true;
        }

        return !policyNames.Any(policyName =>
            policyName.StartsWith("AbpIdentity.", System.StringComparison.Ordinal) ||
            policyName.StartsWith("AbpTenantManagement.", System.StringComparison.Ordinal) ||
            policyName.StartsWith("NoxStock.AuditLogs", System.StringComparison.Ordinal));
    }

    private static IEnumerable<string> GetPolicies(object? resource)
    {
        return resource is HttpContext httpContext
            ? httpContext.GetEndpoint()?.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Select(data => data.Policy)
                .Where(policy => !string.IsNullOrWhiteSpace(policy))
                .Select(policy => policy!) ?? []
            : [];
    }
}
