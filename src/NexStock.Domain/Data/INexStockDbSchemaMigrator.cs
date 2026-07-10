using System.Threading.Tasks;

namespace NexStock.Data;

public interface INexStockDbSchemaMigrator
{
    Task MigrateAsync();
}
