using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NoxStock.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace NoxStock.Web.Pages.TenantManagement.Tenants;

[Authorize("AbpTenantManagement.Tenants")]
public class IndexModel : PageModel
{
    private readonly ITenantRepository _tenantRepository;

    public IndexModel(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    public List<Tenant> Tenants { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Tenants = (await _tenantRepository.GetListAsync())
            .OrderByDescending(tenant => tenant.CreationTime)
            .ToList();
    }
}
