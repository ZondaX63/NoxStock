using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NoxStock.Licensing;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace NoxStock.Web.MultiTenancy;

public sealed class TenantLicenseEnforcementMiddleware
{
    private readonly RequestDelegate _next;

    public TenantLicenseEnforcementMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentTenant currentTenant,
        ITenantLicenseAccessChecker accessChecker,
        IIdentityUserRepository identityUserRepository)
    {
        if (currentTenant.Id.HasValue &&
            context.User.Identity?.IsAuthenticated == true &&
            IsWriteRequest(context.Request) &&
            !IsAuthenticationRoute(context.Request.Path))
        {
            await accessChecker.EnsureCanWriteAsync();

            if (IsIdentityUserCreationRequest(context.Request))
            {
                await accessChecker.EnsureLimitAsync(
                    LicenseLimitNames.MaxUsers,
                    await identityUserRepository.GetCountAsync());
            }
        }

        await _next(context);
    }

    private static bool IsIdentityUserCreationRequest(HttpRequest request)
    {
        return HttpMethods.IsPost(request.Method) &&
               request.Path.StartsWithSegments(
                   new PathString("/api/identity/users"),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWriteRequest(HttpRequest request)
    {
        return !HttpMethods.IsGet(request.Method) &&
               !HttpMethods.IsHead(request.Method) &&
               !HttpMethods.IsOptions(request.Method);
    }

    private static bool IsAuthenticationRoute(PathString path)
    {
        return path.StartsWithSegments(
                   new PathString("/Account"),
                   StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments(
                   new PathString("/api/account"),
                   StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments(
                   new PathString("/Abp"),
                   StringComparison.OrdinalIgnoreCase);
    }
}
