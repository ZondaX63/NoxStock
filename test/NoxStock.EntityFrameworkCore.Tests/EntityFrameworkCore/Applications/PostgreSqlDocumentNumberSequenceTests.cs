using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoxStock.Documents;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace NoxStock.EntityFrameworkCore.Applications;

public class PostgreSqlDocumentNumberSequenceTests : NoxStockEntityFrameworkCoreTestBase
{
    [PostgreSqlFact]
    public Task Two_Concurrent_Allocations_Are_Sequential() => AssertConcurrentAsync(2);

    [PostgreSqlFact]
    public Task Ten_Concurrent_Allocations_Are_Sequential() => AssertConcurrentAsync(10);

    [PostgreSqlFact]
    public Task Hundred_Concurrent_Allocations_Are_Sequential() => AssertConcurrentAsync(100);

    [PostgreSqlFact]
    public async Task Different_Tenant_Type_Year_And_Prefix_Scopes_Are_Independent()
    {
        var tenantB = Guid.NewGuid();
        (await AllocateAsync(NoxStockTestConsts.TenantId, DocumentType.SaleInvoice, 2030, "SF")).ShouldBe(1);
        (await AllocateAsync(NoxStockTestConsts.TenantId, DocumentType.SaleInvoice, 2031, "SF")).ShouldBe(1);
        (await AllocateAsync(NoxStockTestConsts.TenantId, DocumentType.PurchaseInvoice, 2030, "SF")).ShouldBe(1);
        (await AllocateAsync(NoxStockTestConsts.TenantId, DocumentType.SaleInvoice, 2030, "ALT")).ShouldBe(1);
        (await AllocateAsync(tenantB, DocumentType.SaleInvoice, 2030, "SF")).ShouldBe(1);
        (await AllocateAsync(NoxStockTestConsts.TenantId, DocumentType.SaleInvoice, 2030, "SF")).ShouldBe(2);
    }

    [PostgreSqlFact]
    public async Task Rolled_Back_Allocation_Is_Gapless_Because_Sequence_Row_Is_Transactional()
    {
        const int year = 2033;
        const string prefix = "RB";
        using (var scope = ServiceProvider.CreateScope())
        {
            using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(NoxStockTestConsts.TenantId);
            using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
            (await scope.ServiceProvider.GetRequiredService<IDocumentNumberSequenceStore>()
                .TryAllocateAsync(NoxStockTestConsts.TenantId, DocumentType.SaleInvoice, year, prefix)).ShouldBe(1);
            // Deliberately do not complete: allocation and sequence update must roll back together.
        }
        (await AllocateAsync(NoxStockTestConsts.TenantId, DocumentType.SaleInvoice, year, prefix)).ShouldBe(1);
    }

    [PostgreSqlFact]
    public async Task New_Scope_Continues_From_Persisted_Sequence()
    {
        const int year = 2034;
        const string prefix = "RST";
        (await AllocateAsync(NoxStockTestConsts.TenantId, DocumentType.SaleInvoice, year, prefix)).ShouldBe(1);
        // AllocateAsync creates a separate scope/UoW and resolves a fresh store instance.
        (await AllocateAsync(NoxStockTestConsts.TenantId, DocumentType.SaleInvoice, year, prefix)).ShouldBe(2);
    }

    private async Task AssertConcurrentAsync(int count)
    {
        const int year = 2032;
        const string prefix = "CON";
        var values = await Task.WhenAll(Enumerable.Range(0, count)
            .Select(_ => AllocateAsync(NoxStockTestConsts.TenantId, DocumentType.SaleInvoice, year, prefix)));
        values.Distinct().Count().ShouldBe(count);
        values.OrderBy(x => x).ShouldBe(Enumerable.Range(1, count));

        using var scope = ServiceProvider.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(NoxStockTestConsts.TenantId);
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
        var rows = await scope.ServiceProvider.GetRequiredService<IRepository<DocumentNumberSequence, Guid>>()
            .GetListAsync(x => x.DocumentType == DocumentType.SaleInvoice && x.Year == year && x.Prefix == prefix);
        rows.Single().LastNumber.ShouldBe(count);
        await uow.CompleteAsync();
    }

    private async Task<int> AllocateAsync(Guid tenantId, DocumentType type, int year, string prefix)
    {
        using var scope = ServiceProvider.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
        var value = await scope.ServiceProvider.GetRequiredService<IDocumentNumberSequenceStore>().TryAllocateAsync(tenantId, type, year, prefix);
        value.ShouldNotBeNull();
        await uow.CompleteAsync();
        return value!.Value;
    }
}
