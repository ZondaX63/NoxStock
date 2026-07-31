using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using NoxStock.Licensing;
using NoxStock.Localization;
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
        IIdentityUserRepository identityUserRepository,
        IStringLocalizer<NoxStockResource> localizer)
    {
        if (!currentTenant.Id.HasValue ||
            context.User.Identity?.IsAuthenticated != true ||
            IsLicenseAccessRoute(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var access = await accessChecker.GetCurrentAsync();
        if (!access.CanRead)
        {
            await BlockWorkspaceAccessAsync(context, access, localizer);
            return;
        }

        if (IsWriteRequest(context.Request))
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

    private static bool IsLicenseAccessRoute(PathString path)
    {
        return path.StartsWithSegments(
                   new PathString("/Account"),
                   StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments(
                   new PathString("/api/account"),
                   StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments(
                   new PathString("/Abp"),
                   StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments(
                   new PathString("/License/Unavailable"),
                   StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments(
                   new PathString("/api/app/tenant-license-status"),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static Task BlockWorkspaceAccessAsync(
        HttpContext context,
        TenantLicenseAccessDto access,
        IStringLocalizer<NoxStockResource> localizer)
    {
        if (IsApiRequest(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json";
            return context.Response.WriteAsJsonAsync(new
            {
                status = StatusCodes.Status403Forbidden,
                code = LicenseErrorCodes.Required,
                title = localizer["License:AccessBlockedTitle"].Value,
                detail = localizer["License:AccessBlockedDescription"].Value,
                licenseStatus = access.Status?.ToString()
            });
        }

        var destination = context.Request.PathBase.Add("/License/Unavailable");
        context.Response.Redirect(destination.Value!);
        return Task.CompletedTask;
    }

    private static bool IsApiRequest(HttpRequest request)
    {
        return request.Path.StartsWithSegments(
            new PathString("/api"),
            StringComparison.OrdinalIgnoreCase);
    }
}
