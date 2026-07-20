using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Security.Claims;

namespace NoxStock.Security;

public static class TestAuthentication
{
    public const string UserHeader = "X-NoxStock-Test-User";
    public const string TenantHeader = "X-NoxStock-Test-Tenant-Id";

    public static void AuthenticateAs(HttpRequestMessage request, string userName, Guid? tenantId = null)
    {
        request.Headers.Add(UserHeader, userName);
        if (tenantId.HasValue)
        {
            request.Headers.Add(TenantHeader, tenantId.Value.ToString());
        }
    }
}

public sealed class TestAuthenticationStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(async (context, pipelineNext) =>
            {
                if (context.Request.Headers.TryGetValue(TestAuthentication.UserHeader, out var userName) &&
                    !string.IsNullOrWhiteSpace(userName))
                {
                    var claims = new List<Claim>
                    {
                        new(AbpClaimTypes.UserId, Guid.NewGuid().ToString()),
                        new(AbpClaimTypes.UserName, userName.ToString())
                    };

                    if (context.Request.Headers.TryGetValue(TestAuthentication.TenantHeader, out var tenantId) &&
                        Guid.TryParse(tenantId, out var parsedTenantId))
                    {
                        claims.Add(new Claim(AbpClaimTypes.TenantId, parsedTenantId.ToString()));
                    }

                    context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "NoxStockTests"));
                }

                await pipelineNext();
            });

            next(app);
        };
    }
}
