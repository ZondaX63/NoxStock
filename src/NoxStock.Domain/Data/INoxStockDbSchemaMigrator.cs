using System.Threading.Tasks;

namespace NoxStock.Data;

public interface INoxStockDbSchemaMigrator
{
    Task MigrateAsync();
}
