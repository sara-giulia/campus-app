using CampusMarket.Api.Infrastructure.Persistence;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CampusMarket.IntegrationTests.Persistence;

// Runs the migrations by hand on a database of its own (never the one of a test host), so reports can be
// inserted between the migration before AddTrainingExamples and AddTrainingExamples itself.
public sealed class AddTrainingExamplesMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261126135244_AddMessageSpamCheck";
    private const string AddTrainingExamples = "20261126150921_AddTrainingExamples";

    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly string[] PersonalDataWords = ["name", "email", "phone", "password"];
    private static readonly string[] ResolutionColumns = ["Resolution", "ResolvedAt"];

    private readonly string _database = $"campusmarket_migration_{Guid.NewGuid():N}";
    private readonly Guid _member = Guid.NewGuid();
    private readonly Guid _listing = Guid.NewGuid();
    private readonly Guid _userReport = Guid.NewGuid();
    private readonly Guid _automaticReport = Guid.NewGuid();
    private string ConnectionString => SharedPostgres.ConnectionStringFor(_database);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        SharedPostgres.DropDatabase(_database);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Given_ReportsBeforeTheMigration_When_MigratingToAddTrainingExamples_Then_TheyArePending()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertReportsAsync();

        // Act
        await MigrateAsync(AddTrainingExamples);

        // Assert
        var resolutions = await ScalarListAsync("""SELECT "Resolution" FROM "Reports" """);
        resolutions.Should().Equal("Pending", "Pending");
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Reports" WHERE "ResolvedAt" IS NULL""")).Should().Be(2);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "TrainingExamples" """)).Should().Be(0);
    }

    [Fact]
    public async Task Given_MigrationToAddTrainingExamples_When_ReadingTheSchema_Then_ColumnsAndIndexesExistWithoutPersonalData()
    {
        // Act
        await MigrateAsync(AddTrainingExamples);

        // Assert
        var columns = await ColumnsAsync("TrainingExamples");
        columns.Should().BeEquivalentTo(new Dictionary<string, (string Type, string Nullable)>
        {
            ["Id"] = ("uuid", "NO"),
            ["ListingId"] = ("uuid", "NO"),
            ["Kind"] = ("character varying", "NO"),
            ["Title"] = ("character varying", "NO"),
            ["Description"] = ("character varying", "YES"),
            ["Label"] = ("character varying", "NO"),
            ["Source"] = ("character varying", "NO"),
            ["CreatedAt"] = ("timestamp with time zone", "NO"),
            ["CreatedBy"] = ("uuid", "NO"),
            ["UpdatedAt"] = ("timestamp with time zone", "YES"),
        });
        columns.Keys.Should().NotContain(c => PersonalDataWords.Any(w => c.Contains(w, StringComparison.OrdinalIgnoreCase)));

        var reportColumns = await ColumnsAsync("Reports");
        reportColumns["Resolution"].Should().Be(("character varying", "NO"));
        reportColumns["ResolvedAt"].Should().Be(("timestamp with time zone", "YES"));

        (await ScalarAsync<bool>(
            "SELECT indisunique FROM pg_index WHERE indexrelid = 'public.\"IX_TrainingExamples_ListingId_Kind\"'::regclass"))
            .Should().BeTrue();
        var indexes = await ScalarListAsync("SELECT indexname FROM pg_indexes WHERE tablename = 'TrainingExamples'");
        indexes.Should().BeEquivalentTo("PK_TrainingExamples", "IX_TrainingExamples_ListingId_Kind", "IX_TrainingExamples_CreatedAt");
        var reportIndexes = await ScalarListAsync("SELECT indexname FROM pg_indexes WHERE tablename = 'Reports'");
        reportIndexes.Should().Contain("IX_Reports_ListingId_Resolution");
        (await ScalarAsync<long>(
            "SELECT COUNT(*) FROM pg_constraint WHERE conrelid = '\"TrainingExamples\"'::regclass AND contype = 'f'"))
            .Should().Be(0);
    }

    [Fact]
    public async Task Given_AnExample_When_InsertingTheSameListingAndKind_Then_UniqueIndexRefusesItButOtherKindIsAccepted()
    {
        // Arrange
        await MigrateAsync(AddTrainingExamples);
        await InsertExampleAsync("Spam", "spam");

        // Act
        var duplicate = () => InsertExampleAsync("Spam", "not spam");
        await InsertExampleAsync("Category", "Textbook");

        // Assert
        await duplicate.Should().ThrowAsync<PostgresException>().Where(e => e.SqlState == PostgresErrorCodes.UniqueViolation);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "TrainingExamples" """)).Should().Be(2);
    }

    [Fact]
    public async Task Given_ReportsAndExamples_When_MigratingDownToPreviousMigration_Then_TableAndColumnsAreGoneAndReportsRemain()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertReportsAsync();
        await MigrateAsync(AddTrainingExamples);
        await InsertExampleAsync("Spam", "spam");

        // Act
        await MigrateAsync(PreviousMigration);

        // Assert
        (await TableExistsAsync("TrainingExamples")).Should().BeFalse();
        var reportColumns = await ColumnsAsync("Reports");
        reportColumns.Keys.Should().NotContain(ResolutionColumns);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Reports" """)).Should().Be(2);
    }

    private async Task MigrateAsync(string targetMigration)
    {
        var options = new DbContextOptionsBuilder<CampusMarketDbContext>().UseNpgsql(ConnectionString, npgsql => npgsql.UseVector()).Options;
        await using var db = new CampusMarketDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync(targetMigration);
    }

    // A member, a listing, a report by that member and an automatic report (no reporter).
    private async Task InsertReportsAsync()
    {
        await ExecuteAsync(
            """
            INSERT INTO "Members" ("Id", "Name", "Email", "CreatedAt")
            VALUES (@id, 'Ana Popescu', 'ana@student.ro', @createdAt)
            """,
            ("id", _member), ("createdAt", T0));
        await ExecuteAsync(
            """
            INSERT INTO "Listings" ("Id", "SellerId", "Title", "Category", "Condition", "Price", "Status", "Version", "CreatedAt")
            VALUES (@id, @seller, 'Manual POO', 'Textbook', 'Good', 50, 'Active', 1, @createdAt)
            """,
            ("id", _listing), ("seller", Guid.NewGuid()), ("createdAt", T0));
        await ExecuteAsync(
            """
            INSERT INTO "Reports" ("Id", "ListingId", "ReporterId", "Reason", "SpamProbability", "CreatedAt")
            VALUES (@user, @listing, @member, 'Pare o inselatorie', NULL, @createdAt),
                   (@automatic, @listing, NULL, 'Flagged automatically as spam.', 0.9, @createdAt)
            """,
            ("user", _userReport), ("automatic", _automaticReport), ("listing", _listing), ("member", _member), ("createdAt", T0));
    }

    private async Task InsertExampleAsync(string kind, string label) =>
        await ExecuteAsync(
            """
            INSERT INTO "TrainingExamples" ("Id", "ListingId", "Kind", "Title", "Label", "Source", "CreatedAt", "CreatedBy")
            VALUES (@id, @listing, @kind, 'Manual POO', @label, 'Report', @createdAt, @createdBy)
            """,
            ("id", Guid.NewGuid()), ("listing", _listing), ("kind", kind), ("label", label),
            ("createdAt", T0), ("createdBy", Guid.NewGuid()));

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
