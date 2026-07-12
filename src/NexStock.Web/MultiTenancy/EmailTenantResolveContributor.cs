using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace NexStock.Web.MultiTenancy;

public class EmailTenantResolveContributor : TenantResolveContributorBase
{
    public override string Name => "Email";

    public override async Task ResolveAsync(ITenantResolveContext context)
    {
        var httpContext = context.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext;
        if (httpContext == null) return;

        // Sadece /Account/Login'e yapılan POST isteklerinde çalış
        if (httpContext.Request.Path.StartsWithSegments("/Account/Login", StringComparison.OrdinalIgnoreCase) && 
            HttpMethods.IsPost(httpContext.Request.Method))
        {
            if (httpContext.Request.HasFormContentType && httpContext.Request.Form.TryGetValue("LoginInput.UserNameOrEmailAddress", out var emailValues))
            {
                var email = emailValues.ToString();
                if (email.Contains("@"))
                {
                    var userRepository = context.ServiceProvider.GetRequiredService<IIdentityUserRepository>();
                    var dataFilter = context.ServiceProvider.GetRequiredService<IDataFilter>();

                    using (dataFilter.Disable<IMultiTenant>())
                    {
                        var users = await userRepository.GetListAsync();
                        var user = users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
                        
                        if (user != null && user.TenantId.HasValue)
                        {
                            context.TenantIdOrName = user.TenantId.Value.ToString();
                            context.Handled = true;
                        }
                    }
                }
            }
        }
    }
}
