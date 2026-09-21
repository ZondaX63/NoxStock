using System;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;

namespace NoxStock.EntityFrameworkCore;

[DependsOn(
    typeof(NoxStockApplicationTestModule),
    typeof(NoxStockEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqliteModule)
)]
public class NoxStockEntityFrameworkCoreTestModule : AbpModule
{
    private const string PostgreSqlTestConnectionStringEnvironmentVariable = "NOXSTOCK_POSTGRES_TEST_CONNECTION";
    private SqliteConnection? _sqliteConnection;

    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<AbpSqliteOptions>(x => x.BusyTimeout = null);
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<FeatureManagementOptions>(options =>
        {
            options.SaveStaticFeaturesToDatabase = false;
            options.IsDynamicFeatureStoreEnabled = false;
        });
        Configure<PermissionManagementOptions>(options =>
        {
            options.SaveStaticPermissionsToDatabase = false;
            options.IsDynamicPermissionStoreEnabled = false;
        });
        var postgreSqlConnectionString = Environment.GetEnvironmentVariable(PostgreSqlTestConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(postgreSqlConnectionString))
        {
            context.Services.AddAlwaysDisableUnitOfWorkTransaction();
            ConfigureInMemorySqlite(context.Services);
            return;
        }

        ConfigurePostgreSql(context.Services, postgreSqlConnectionString);

    }

    private void ConfigureInMemorySqlite(IServiceCollection services)
    {
        _sqliteConnection = CreateDatabaseAndGetConnection();

        services.Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(context =>
            {
                context.DbContextOptions.UseSqlite(_sqliteConnection).AddInterceptors(SqlCommandCounter.Instance);
            });
        });
    }

    private static SqliteConnection CreateDatabaseAndGetConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<NoxStockDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var context = new NoxStockDbContext(options))
        {
            context.GetService<IRelationalDatabaseCreator>().CreateTables();
        }

        return connection;
    }

    private static void ConfigurePostgreSql(IServiceCollection services, string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.Database, "noxstock_concurrency_test", StringComparison.Ordinal) ||
            !string.Equals(builder.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "NOXSTOCK_POSTGRES_TEST_CONNECTION must target localhost/noxstock_concurrency_test.");
        }

        var options = new DbContextOptionsBuilder<NoxStockDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        using (var context = new NoxStockDbContext(options))
        {
            context.Database.EnsureDeleted();
            context.Database.Migrate();
        }

        services.Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(context => context.DbContextOptions.UseNpgsql(connectionString));
        });
    }
}
