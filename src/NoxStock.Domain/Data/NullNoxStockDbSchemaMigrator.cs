using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace NoxStock.Data;

/* This is used if database provider does't define
 * INoxStockDbSchemaMigrator implementation.
 */
public class NullNoxStockDbSchemaMigrator : INoxStockDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
