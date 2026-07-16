using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using AbpIdentityUser = Volo.Abp.Identity.IdentityUser;

namespace NoxStock.Web.Pages.Account;

public class LogoutModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
{
    private readonly SignInManager<AbpIdentityUser> _signInManager;
    private readonly ICurrentTenant _currentTenant;

    public LogoutModel(
        SignInManager<AbpIdentityUser> signInManager,
        ICurrentTenant currentTenant)
    {
        _signInManager = signInManager;
        _currentTenant = currentTenant;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _signInManager.SignOutAsync();

        var destination = _currentTenant.Id.HasValue ? "/Account/Login" : "/Account/Login";
        return LocalRedirect(Request.PathBase.Add(destination).Value!);
    }
}
