using CampusMarket.Api.Infrastructure.Persistence;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CampusMarket.IntegrationTests.Persistence;

// Runs the migrations by hand on a database of its own (never the one of a test host), so a photo row can be
// inserted between the migration before AddPhotoThumbnails and AddPhotoThumbnails itself.
public sealed class AddPhotoThumbnailsMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261126192341_AddConversationReadState";
    private const string AddPhotoThumbnails = "20261127134622_AddPhotoThumbnails";

    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly string _database = $"campusmarket_migration_{Guid.NewGuid():N}";
    private readonly Guid _seller = Guid.NewGuid();
    private readonly Guid _listing = Guid.NewGuid();
    private readonly Guid _photo = Guid.NewGuid();
    private string ConnectionString => SharedPostgres.ConnectionStringFor(_database);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        SharedPostgres.DropDatabase(_database);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Given_PhotoBeforeTheMigration_When_MigratingToAddPhotoThumbnails_Then_ColumnExistsAndOldRowHasNull()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertPhotoAsync();

        // Act
        await MigrateAsync(AddPhotoThumbnails);

        // Assert
        var columns = await ColumnsAsync("Photos");
        columns["ThumbnailFileName"].Should().Be(("character varying", 64, "YES"));

        var thumbnail = await ScalarAsync<object>(
            """SELECT "ThumbnailFileName" FROM "Photos" WHERE "Id" = @id""", ("id", _photo));
        thumbnail.Should().Be(DBNull.Value);
        (await ScalarAsync<string>("""SELECT "FileName" FROM "Photos" WHERE "Id" = @id""", ("id", _photo)))
            .Should().Be($"{_photo}.png");
    }

    [Fact]
    public async Task Given_MigratedDatabase_When_MigratingDownToAddConversationReadState_Then_ColumnIsGoneAndRowRemains()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertPhotoAsync();
        await MigrateAsync(AddPhotoThumbnails);

        // Act
        await MigrateAsync(PreviousMigration);

        // Assert
        var columns = await ColumnsAsync("Photos");
        columns.Should().NotContainKey("ThumbnailFileName");
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Photos" """)).Should().Be(1);
        (await ScalarAsync<string>("""SELECT "FileName" FROM "Photos" WHERE "Id" = @id""", ("id", _photo)))
            .Should().Be($"{_photo}.png");
    }

    private async Task MigrateAsync(string targetMigration)
    {
        var options = new DbContextOptionsBuilder<CampusMarketDbContext>().UseNpgsql(ConnectionString, npgsql => npgsql.UseVector()).Options;
        await using var db = new CampusMarketDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync(targetMigration);
    }

    // A member, a listing and one photo row from before the thumbnails.
    private async Task InsertPhotoAsync()
    {
        await ExecuteAsync(
            """
            INSERT INTO "Members" ("Id", "Name", "Email", "CreatedAt")
            VALUES (@id, 'Ion Ionescu', 'ion@student.ro', @createdAt)
            """,
            ("id", _seller), ("createdAt", T0));
        await ExecuteAsync(
            """
            INSERT INTO "Listings" ("Id", "SellerId", "Title", "Category", "Condition", "Price", "Status", "Version", "CreatedAt")
            VALUES (@id, @seller, 'Manual POO', 'Textbook', 'Good', 50, 'Active', 2, @createdAt)
            """,
            ("id", _listing), ("seller", _seller), ("createdAt", T0));
        await ExecuteAsync(
            """
            INSERT INTO "Photos" ("Id", "ListingId", "FileName", "ContentType", "SizeBytes", "CreatedAt", "IsGenerated", "Position")
            VALUES (@id, @listing, @fileName, 'image/png', 16, @createdAt, false, 0)
            """,
            ("id", _photo), ("listing", _listing), ("fileName", $"{_photo}.png"), ("createdAt", T0));
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

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    // Column name -> (data type, max length, is_nullable).
    private async Task<Dictionary<string, (string Type, int? Length, string Nullable)>> ColumnsAsync(string table)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT column_name, data_type, character_maximum_length, is_nullable FROM information_schema.columns WHERE table_name = @t",
            connection);
        command.Parameters.AddWithValue("t", table);
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new Dictionary<string, (string, int?, string)>();
        while (await reader.ReadAsync())
            columns[reader.GetString(0)] = (reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetInt32(2), reader.GetString(3));
        return columns;
    }
}
