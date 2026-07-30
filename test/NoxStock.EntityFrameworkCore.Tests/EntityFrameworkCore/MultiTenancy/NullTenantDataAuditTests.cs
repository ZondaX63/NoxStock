using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.Products;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace NoxStock.EntityFrameworkCore.MultiTenancy;

[Collection(NoxStockTestConsts.CollectionDefinitionName)]
public class NullTenantDataAuditTests : NoxStockEntityFrameworkCoreTestBase
{
    private readonly ICurrentTenant _currentTenant;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly NullTenantDataAuditService _auditService;

    public NullTenantDataAuditTests()
    {
        _currentTenant = ServiceProvider.GetRequiredService<ICurrentTenant>();
        _productRepository = ServiceProvider.GetRequiredService<IRepository<Product, Guid>>();
        _auditService = ServiceProvider.GetRequiredService<NullTenantDataAuditService>();
    }

    [Fact]
    public async Task Scan_Should_Report_Null_Tenant_Rows_Without_Changing_Ownership()
    {
        var product = new Product(Guid.NewGuid(), null, "Unowned Product", "UNOWNED-001")
        {
            Barcode = string.Empty,
            Description = string.Empty,
            Unit = "Adet",
            ShelfLocation = string.Empty,
            PurchaseCurrency = "TRY",
        };

        using (_currentTenant.Change(null))
        {
            await _productRepository.InsertAsync(product, autoSave: true);

            var report = await _auditService.ScanAsync();

            var productAudit = report.Single(x => x.Table == "AppProducts");
            productAudit.RowCount.ShouldBe(1);
            productAudit.SampleIds.ShouldContain(product.Id);
            (await _productRepository.GetAsync(product.Id)).TenantId.ShouldBeNull();
        }
    }
}
