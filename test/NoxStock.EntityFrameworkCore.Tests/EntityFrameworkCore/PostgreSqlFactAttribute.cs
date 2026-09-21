using System;
using Xunit;

namespace NoxStock.EntityFrameworkCore;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NOXSTOCK_POSTGRES_TEST_CONNECTION")))
        {
            Skip = "Requires the isolated PostgreSQL Compose profile. Run run-postgresql-commercial-concurrency-tests.sh.";
        }
    }
}
