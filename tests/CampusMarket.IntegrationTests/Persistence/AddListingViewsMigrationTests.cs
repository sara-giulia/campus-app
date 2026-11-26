using CampusMarket.Api.Infrastructure.Persistence;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CampusMarket.IntegrationTests.Persistence;

// Runs the migrations by hand on a database of its own (never the one of a test host), so a listing can be
// inserted between the migration before AddListingViews and AddListingViews itself.
public sealed class AddListingViewsMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261125214900_AddListingEmbeddings";
    private const string AddListingViews = "20261126112225_AddListingViews";

    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly string _database = $"campusmarket_migration_{Guid.NewGuid():N}";
    private readonly Guid _listing = Guid.NewGuid();
    private string ConnectionString => SharedPostgres.ConnectionStringFor(_database);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        SharedPostgres.DropDatabase(_database);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Given_AListingBeforeTheMigration_When_MigratingToAddListingViews_Then_ListingStaysAndTableExists()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertListingAsync();
        (await TableExistsAsync("ListingViews")).Should().BeFalse();

        // Act
        await MigrateAsync(AddListingViews);

        // Assert
        (await TableExistsAsync("ListingViews")).Should().BeTrue();
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Listings" """)).Should().Be(1);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "ListingViews" """)).Should().Be(0);
    }

    [Fact]
    public async Task Given_MigrationToAddListingViews_When_ReadingTheSchema_Then_ColumnsIndexAndForeignKeyExist()
    {
        // Act
        await MigrateAsync(AddListingViews);

        // Assert
        var columns = await ColumnsAsync("ListingViews");
        columns.Should().BeEquivalentTo(new Dictionary<string, (string Type, string Nullable)>
        {
            ["Id"] = ("uuid", "NO"),
            ["ListingId"] = ("uuid", "NO"),
            ["ViewerKey"] = ("character varying", "NO"),
            ["Day"] = ("date", "NO"),
            ["CreatedAt"] = ("timestamp with time zone", "NO"),
        });
        (await ScalarAsync<int>(
            "SELECT character_maximum_length FROM information_schema.columns WHERE table_name = 'ListingViews' AND column_name = 'ViewerKey'"))
            .Should().Be(64);
        var indexes = await ScalarListAsync("SELECT indexname FROM pg_indexes WHERE tablename = 'ListingViews'");
        indexes.Should().Contain("IX_ListingViews_ListingId_ViewerKey_Day");
        (await ScalarAsync<bool>(
            "SELECT indisunique FROM pg_index WHERE indexrelid = 'public.\"IX_ListingViews_ListingId_ViewerKey_Day\"'::regclass"))
            .Should().BeTrue();
        var foreignKeys = await ScalarListAsync(
            "SELECT conname FROM pg_constraint WHERE conrelid = '\"ListingViews\"'::regclass AND contype = 'f'");
        foreignKeys.Should().BeEquivalentTo("FK_ListingViews_Listings_ListingId");
    }

    [Fact]
    public async Task Given_AView_When_InsertingTheSameListingKeyAndDay_Then_UniqueIndexRefusesItButOtherDayOrKeyIsAccepted()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertListingAsync();
        await MigrateAsync(AddListingViews);
        await InsertViewAsync("v:abc", new DateOnly(2026, 10, 1));

        // Act
        var duplicate = () => InsertViewAsync("v:abc", new DateOnly(2026, 10, 1));
        await InsertViewAsync("v:abc", new DateOnly(2026, 10, 2));
        await InsertViewAsync("v:def", new DateOnly(2026, 10, 1));

        // Assert
        await duplicate.Should().ThrowAsync<PostgresException>().Where(e => e.SqlState == PostgresErrorCodes.UniqueViolation);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "ListingViews" """)).Should().Be(3);
    }

    [Fact]
    public async Task Given_ViewWithUnknownListing_When_Inserting_Then_ForeignKeyRefusesIt()
    {
        // Arrange
        await MigrateAsync(AddListingViews);

        // Act
        var act = () => InsertViewAsync("v:abc", new DateOnly(2026, 10, 1), Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<PostgresException>().Where(e => e.SqlState == PostgresErrorCodes.ForeignKeyViolation);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "ListingViews" """)).Should().Be(0);
    }

    [Fact]
    public async Task Given_Views_When_DeletingTheListing_Then_TheirViewsAreDeleted()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertListingAsync();
        await MigrateAsync(AddListingViews);
        await InsertViewAsync("v:abc", new DateOnly(2026, 10, 1));
        await InsertViewAsync("v:def", new DateOnly(2026, 10, 1));

        // Act
        await ExecuteAsync("""DELETE FROM "Listings" WHERE "Id" = @id""", ("id", _listing));

        // Assert
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "ListingViews" """)).Should().Be(0);
    }

    [Fact]
    public async Task Given_Views_When_MigratingDownToPreviousMigration_Then_TableIsGoneAndListingRemains()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertListingAsync();
        await MigrateAsync(AddListingViews);
        await InsertViewAsync("v:abc", new DateOnly(2026, 10, 1));

        // Act
        await MigrateAsync(PreviousMigration);

        // Assert
        (await TableExistsAsync("ListingViews")).Should().BeFalse();
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Listings" """)).Should().Be(1);
    }

    private async Task MigrateAsync(string targetMigration)
    {
        var options = new DbContextOptionsBuilder<CampusMarketDbContext>().UseNpgsql(ConnectionString, npgsql => npgsql.UseVector()).Options;
        await using var db = new CampusMarketDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync(targetMigration);
    }

    private async Task InsertListingAsync() =>
        await ExecuteAsync(
            """
            INSERT INTO "Listings" ("Id", "SellerId", "Title", "Category", "Condition", "Price", "Status", "Version", "CreatedAt")
            VALUES (@id, @seller, 'Manual POO', 'Textbook', 'Good', 50, 'Active', 1, @createdAt)
            """,
            ("id", _listing), ("seller", Guid.NewGuid()), ("createdAt", T0));

    private async Task InsertViewAsync(string key, DateOnly day, Guid? listingId = null) =>
        await ExecuteAsync(
            """
            INSERT INTO "ListingViews" ("Id", "ListingId", "ViewerKey", "Day", "CreatedAt")
            VALUES (@id, @listing, @key, @day, @createdAt)
            """,
            ("id", Guid.NewGuid()), ("listing", listingId ?? _listing), ("key", key), ("day", day), ("createdAt", T0));

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private async Task<List<string>> ScalarListAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync())
            values.Add(reader.GetString(0));
        return values;
    }

    private async Task<bool> TableExistsAsync(string table) =>
        await ScalarAsync<long>("SELECT COUNT(*) FROM information_schema.tables WHERE table_name = @t", ("t", table)) > 0;

    // Column name -> (data type, is_nullable).
    private async Task<Dictionary<string, (string Type, string Nullable)>> ColumnsAsync(string table)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT column_name, data_type, is_nullable FROM information_schema.columns WHERE table_name = @t", connection);
        command.Parameters.AddWithValue("t", table);
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new Dictionary<string, (string, string)>();
        while (await reader.ReadAsync())
            columns[reader.GetString(0)] = (reader.GetString(1), reader.GetString(2));
        return columns;
    }
}
