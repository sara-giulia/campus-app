using CampusMarket.Api.Infrastructure.Persistence;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CampusMarket.IntegrationTests.Persistence;

// Runs the migrations by hand on a database of its own (never the one of a test host), so the data can be
// inserted between the migration before AddPhotoPosition and AddPhotoPosition itself.
public sealed class AddPhotoPositionMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261124163450_InitialPostgres";
    private const string AddPhotoPosition = "20261125102536_AddPhotoPosition";

    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly string _database = $"campusmarket_migration_{Guid.NewGuid():N}";
    private string ConnectionString => SharedPostgres.ConnectionStringFor(_database);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        SharedPostgres.DropDatabase(_database);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Given_PhotosBeforeTheMigration_When_MigratingToAddPhotoPosition_Then_NumbersEachListingByCreatedAtThenId()
    {
        // Arrange: the schema before AddPhotoPosition, with photos in two listings.
        await MigrateAsync(PreviousMigration);
        var listingA = Guid.NewGuid();
        var listingB = Guid.NewGuid();
        var a1 = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
        var a2 = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
        var a3 = Guid.Parse("00000000-0000-0000-0000-0000000000a3");
        var b1 = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
        var b2 = Guid.Parse("00000000-0000-0000-0000-0000000000b2");
        await InsertListingAsync(listingA);
        await InsertListingAsync(listingB);
        // Listing A: a3 is the oldest, a1 and a2 have the same CreatedAt (the Id decides).
        await InsertPhotoAsync(a2, listingA, T0.AddHours(1));
        await InsertPhotoAsync(a1, listingA, T0.AddHours(1));
        await InsertPhotoAsync(a3, listingA, T0);
        // Listing B: b2 is older than b1; a different listing starts from 0 again.
        await InsertPhotoAsync(b1, listingB, T0.AddHours(5));
        await InsertPhotoAsync(b2, listingB, T0.AddHours(2));
        var createdBefore = await ReadPhotosAsync();

        // Act
        await MigrateAsync(AddPhotoPosition);

        // Assert
        var after = await ReadPhotosAsync();
        after.Should().HaveCount(5);
        after[a3].Position.Should().Be(0);
        after[a1].Position.Should().Be(1);
        after[a2].Position.Should().Be(2);
        after[b2].Position.Should().Be(0);
        after[b1].Position.Should().Be(1);
        foreach (var (id, photo) in after)
            photo.CreatedAt.Should().Be(createdBefore[id].CreatedAt);
    }

    private async Task MigrateAsync(string targetMigration)
    {
        var options = new DbContextOptionsBuilder<CampusMarketDbContext>().UseNpgsql(ConnectionString).Options;
        await using var db = new CampusMarketDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync(targetMigration);
    }

    private async Task InsertListingAsync(Guid id) =>
        await ExecuteAsync(
            """
            INSERT INTO "Listings" ("Id", "SellerId", "Title", "Category", "Condition", "Price", "Status", "Version", "CreatedAt")
            VALUES (@id, @seller, 'Manual POO', 'Textbook', 'Good', 50, 'Active', 1, @createdAt)
            """,
            ("id", id), ("seller", Guid.NewGuid()), ("createdAt", T0));

    private async Task InsertPhotoAsync(Guid id, Guid listingId, DateTimeOffset createdAt) =>
        await ExecuteAsync(
            """
            INSERT INTO "Photos" ("Id", "ListingId", "FileName", "ContentType", "SizeBytes", "CreatedAt", "IsGenerated")
            VALUES (@id, @listing, @fileName, 'image/png', 16, @createdAt, false)
            """,
            ("id", id), ("listing", listingId), ("fileName", $"{id}.png"), ("createdAt", createdAt));

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    // Reads the photos with raw SQL; Position is null before the migration added the column.
    private async Task<Dictionary<Guid, (int? Position, DateTimeOffset CreatedAt)>> ReadPhotosAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var hasPosition = await ColumnExistsAsync(connection);
        var sql = hasPosition
            ? "SELECT \"Id\", \"Position\", \"CreatedAt\" FROM \"Photos\""
            : "SELECT \"Id\", NULL::integer, \"CreatedAt\" FROM \"Photos\"";
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var photos = new Dictionary<Guid, (int? Position, DateTimeOffset CreatedAt)>();
        while (await reader.ReadAsync())
            photos[reader.GetGuid(0)] = (reader.IsDBNull(1) ? null : reader.GetInt32(1), reader.GetFieldValue<DateTimeOffset>(2));
        return photos;
    }

    private static async Task<bool> ColumnExistsAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'Photos' AND column_name = 'Position'", connection);
        return (long)(await command.ExecuteScalarAsync())! > 0;
    }
}
