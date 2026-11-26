using CampusMarket.Api.Infrastructure.Persistence;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Persistence;

// Runs the migrations by hand on a database of its own (never the one of a test host), to check what
// AddListingEmbeddings creates and that going back to AddPhotoPosition removes it.
public sealed class AddListingEmbeddingsMigrationTests : IAsyncLifetime
{
    private const string AddPhotoPosition = "20261125102536_AddPhotoPosition";
    private const string AddListingEmbeddings = "20261125214900_AddListingEmbeddings";

    private static readonly string[] ExpectedColumns = ["Embedding", "IndexedAt", "ListingId", "Model", "TextHash"];

    private readonly string _database = $"campusmarket_migration_{Guid.NewGuid():N}";
    private string ConnectionString => SharedPostgres.ConnectionStringFor(_database);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        SharedPostgres.DropDatabase(_database);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Given_SchemaBeforeTheMigration_When_MigratingToAddListingEmbeddings_Then_CreatesTheTableWithItsHnswIndex()
    {
        // Arrange
        await MigrateAsync(AddPhotoPosition);

        // Act
        await MigrateAsync(AddListingEmbeddings);

        // Assert
        (await ScalarAsync<long>("SELECT COUNT(*) FROM pg_extension WHERE extname = 'vector'")).Should().Be(1);
        (await ColumnsAsync()).Should().Equal(ExpectedColumns);

        var indexDef = await ScalarAsync<string>(
            "SELECT indexdef FROM pg_indexes WHERE tablename = 'ListingEmbeddings' AND indexname = 'IX_ListingEmbeddings_Embedding'");
        indexDef.Should().Contain("hnsw").And.Contain("vector_cosine_ops");
    }

    [Fact]
    public async Task Given_AddListingEmbeddingsApplied_When_MigratingDownToAddPhotoPosition_Then_RemovesTheTable()
    {
        // Arrange
        await MigrateAsync(AddListingEmbeddings);

        // Act
        await MigrateAsync(AddPhotoPosition);

        // Assert
        (await ScalarAsync<long>("SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'ListingEmbeddings'"))
            .Should().Be(0);
        (await ScalarAsync<long>("SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'Photos'"))
            .Should().Be(1);
    }

    private async Task MigrateAsync(string targetMigration)
    {
        var options = new DbContextOptionsBuilder<CampusMarketDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.UseVector()).Options;
        await using var db = new CampusMarketDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync(targetMigration);
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private async Task<List<string>> ColumnsAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT column_name FROM information_schema.columns WHERE table_name = 'ListingEmbeddings' ORDER BY column_name", connection);
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(0));
        return columns;
    }
}
