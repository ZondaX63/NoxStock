using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace NexStock.Data;

/* This is used if database provider does't define
 * INexStockDbSchemaMigrator implementation.
 */
public class NullNexStockDbSchemaMigrator : INexStockDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
