using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NoxStock.Licensing;

namespace NoxStock.Web.Pages.License;

[Authorize]
public class UnavailableModel : PageModel
{
    private readonly ITenantLicenseAccessChecker _licenseAccessChecker;

    public UnavailableModel(ITenantLicenseAccessChecker licenseAccessChecker)
    {
        _licenseAccessChecker = licenseAccessChecker;
    }

    public TenantLicenseAccessDto License { get; private set; } = new();

    public async Task OnGetAsync()
    {
        License = await _licenseAccessChecker.GetCurrentAsync();
    }
}
