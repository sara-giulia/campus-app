using CampusMarket.Api.Infrastructure.Persistence;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CampusMarket.IntegrationTests.Persistence;

// Runs the migrations by hand on a database of its own (never the one of a test host), so the data can be
// inserted between the migration before AddListingExpiry and AddListingExpiry itself.
public sealed class AddListingExpiryMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261125102536_AddPhotoPosition";
    private const string AddListingExpiry = "20261125143000_AddListingExpiry";

    // The test and the PostgreSQL container can disagree by a few seconds (Docker Desktop clock drift).
    private static readonly TimeSpan ClockSlack = TimeSpan.FromMinutes(1);

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
    public async Task Given_ListingsInEveryStatus_When_MigratingToAddListingExpiry_Then_OnlyActiveAndReservedGetExpiresAtThirtyDaysAhead()
    {
        // Arrange: the schema before AddListingExpiry, with one listing per status.
        await MigrateAsync(PreviousMigration);
        var ids = new Dictionary<string, Guid>();
        foreach (var status in new[] { "Draft", "Active", "Reserved", "Sold", "Hidden" })
        {
            ids[status] = Guid.NewGuid();
            await InsertListingAsync(ids[status], status);
        }

        var before = await ReadListingsAsync(hasExpiresAt: false);

        // Act
        var started = DateTimeOffset.UtcNow;
        await MigrateAsync(AddListingExpiry);
        var finished = DateTimeOffset.UtcNow;

        // Assert
        var after = await ReadListingsAsync(hasExpiresAt: true);
        after.Should().HaveCount(5);

        foreach (var status in new[] { "Active", "Reserved" })
        {
            var expiresAt = after[ids[status]].ExpiresAt;
            expiresAt.Should().NotBeNull($"a {status} listing gets a full period from the migration");
            expiresAt!.Value.Should().BeOnOrAfter(started.AddDays(30) - ClockSlack);
            expiresAt.Value.Should().BeOnOrBefore(finished.AddDays(30) + ClockSlack);
        }

        foreach (var status in new[] { "Draft", "Sold", "Hidden" })
            after[ids[status]].ExpiresAt.Should().BeNull($"a {status} listing never expires");

        foreach (var (status, id) in ids)
        {
            after[id].Status.Should().Be(status);
            after[id].Title.Should().Be(before[id].Title);
            after[id].Price.Should().Be(before[id].Price);
            after[id].Version.Should().Be(before[id].Version);
            after[id].CreatedAt.Should().Be(before[id].CreatedAt);
        }
    }

    [Fact]
    public async Task Given_ExpiredListing_When_MigratingDownToPreviousMigration_Then_ListingBecomesActiveAndColumnIsDropped()
    {
        // Arrange: the schema with AddListingExpiry, with an expired and a sold listing.
        await MigrateAsync(AddListingExpiry);
        var expired = Guid.NewGuid();
        var sold = Guid.NewGuid();
        await InsertListingAsync(expired, "Expired", expiresAt: DateTimeOffset.UtcNow.AddDays(-1));
        await InsertListingAsync(sold, "Sold");

        // Act
        await MigrateAsync(PreviousMigration);

        // Assert
        (await ColumnExistsAsync()).Should().BeFalse();
        var after = await ReadListingsAsync(hasExpiresAt: false);
        after[expired].Status.Should().Be("Active");
        after[expired].Version.Should().Be(1);
        after[sold].Status.Should().Be("Sold");
    }

    private async Task MigrateAsync(string targetMigration)
    {
        var options = new DbContextOptionsBuilder<CampusMarketDbContext>().UseNpgsql(ConnectionString, npgsql => npgsql.UseVector()).Options;
        await using var db = new CampusMarketDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync(targetMigration);
    }

    private async Task InsertListingAsync(Guid id, string status, DateTimeOffset? expiresAt = null)
    {
        if (expiresAt is null)
        {
            await ExecuteAsync(
                """
                INSERT INTO "Listings" ("Id", "SellerId", "Title", "Category", "Condition", "Price", "Status", "Version", "CreatedAt")
                VALUES (@id, @seller, @title, 'Textbook', 'Good', 50, @status, 3, @createdAt)
                """,
                ("id", id), ("seller", Guid.NewGuid()), ("title", $"Manual {status}"), ("status", status), ("createdAt", T0));
            return;
        }

        await ExecuteAsync(
            """
            INSERT INTO "Listings" ("Id", "SellerId", "Title", "Category", "Condition", "Price", "Status", "Version", "CreatedAt", "ExpiresAt")
            VALUES (@id, @seller, @title, 'Textbook', 'Good', 50, @status, 1, @createdAt, @expiresAt)
            """,
            ("id", id), ("seller", Guid.NewGuid()), ("title", $"Manual {status}"), ("status", status),
            ("createdAt", T0), ("expiresAt", expiresAt.Value));
    }

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    // Reads the listings with raw SQL; ExpiresAt exists only after AddListingExpiry.
    private async Task<Dictionary<Guid, ListingRow>> ReadListingsAsync(bool hasExpiresAt)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var sql = hasExpiresAt
            ? "SELECT \"Id\", \"Title\", \"Price\", \"Status\", \"Version\", \"CreatedAt\", \"ExpiresAt\" FROM \"Listings\""
            : "SELECT \"Id\", \"Title\", \"Price\", \"Status\", \"Version\", \"CreatedAt\", NULL::timestamptz FROM \"Listings\"";
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new Dictionary<Guid, ListingRow>();
        while (await reader.ReadAsync())
        {
            rows[reader.GetGuid(0)] = new ListingRow(
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetFieldValue<DateTimeOffset>(5),
                reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6));
        }

        return rows;
    }

    private async Task<bool> ColumnExistsAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'Listings' AND column_name = 'ExpiresAt'", connection);
        return (long)(await command.ExecuteScalarAsync())! > 0;
    }

    private sealed record ListingRow(
        string Title, decimal Price, string Status, int Version, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);
}
