using Npgsql;
using Testcontainers.PostgreSql;

namespace CampusMarket.IntegrationTests.Infrastructure;

// One PostgreSQL container for the whole test run, started on first use (Docker must be running).
// Every CampusMarketApiFactory creates its own database on it; Testcontainers' resource reaper removes the container when the run ends.
internal static class SharedPostgres
{
    private static readonly Lazy<PostgreSqlContainer> Container = new(StartContainer, LazyThreadSafetyMode.ExecutionAndPublication);

    private static PostgreSqlContainer StartContainer()
    {
        var container = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();
        container.StartAsync().GetAwaiter().GetResult();
        return container;
    }

    // Connection string for a new, unique database on the shared container. Pooling is off so no connection lingers after the test.
    public static string ConnectionStringFor(string database)
    {
        var builder = new NpgsqlConnectionStringBuilder(Container.Value.GetConnectionString())
        {
            Database = database,
            Pooling = false
        };
        return builder.ConnectionString;
    }

    public static void DropDatabase(string database)
    {
        var builder = new NpgsqlConnectionStringBuilder(Container.Value.GetConnectionString()) { Pooling = false };
        using var connection = new NpgsqlConnection(builder.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)";
        command.ExecuteNonQuery();
    }
}
