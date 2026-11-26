using CampusMarket.Api.Infrastructure.Persistence;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CampusMarket.IntegrationTests.Persistence;

// Runs the migrations by hand on a database of its own (never the one of a test host), so the members can be
// inserted between the migration before AddMemberBlocks and AddMemberBlocks itself.
public sealed class AddMemberBlocksMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261125170000_AddOffers";
    private const string AddMemberBlocks = "20261125190000_AddMemberBlocks";

    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly string _database = $"campusmarket_migration_{Guid.NewGuid():N}";
    private readonly Guid _first = Guid.NewGuid();
    private readonly Guid _second = Guid.NewGuid();
    private string ConnectionString => SharedPostgres.ConnectionStringFor(_database);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        SharedPostgres.DropDatabase(_database);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Given_MembersBeforeTheMigration_When_MigratingToAddMemberBlocks_Then_MembersStayAndTableExists()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertMembersAsync();
        (await TableExistsAsync("MemberBlocks")).Should().BeFalse();

        // Act
        await MigrateAsync(AddMemberBlocks);

        // Assert
        (await TableExistsAsync("MemberBlocks")).Should().BeTrue();
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Members" """)).Should().Be(2);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "MemberBlocks" """)).Should().Be(0);
    }

    [Fact]
    public async Task Given_MigrationToAddMemberBlocks_When_ReadingTheSchema_Then_ColumnsIndexesAndForeignKeysExist()
    {
        // Act
        await MigrateAsync(AddMemberBlocks);

        // Assert
        var columns = await ColumnsAsync("MemberBlocks");
        columns.Should().BeEquivalentTo(new Dictionary<string, (string Type, string Nullable)>
        {
            ["Id"] = ("uuid", "NO"),
            ["BlockerId"] = ("uuid", "NO"),
            ["BlockedId"] = ("uuid", "NO"),
            ["CreatedAt"] = ("timestamp with time zone", "NO"),
        });
        var indexes = await ScalarListAsync("SELECT indexname FROM pg_indexes WHERE tablename = 'MemberBlocks'");
        indexes.Should().Contain(["IX_MemberBlocks_BlockerId_BlockedId", "IX_MemberBlocks_BlockedId"]);
        var foreignKeys = await ScalarListAsync(
            "SELECT conname FROM pg_constraint WHERE conrelid = '\"MemberBlocks\"'::regclass AND contype = 'f'");
        foreignKeys.Should().BeEquivalentTo("FK_MemberBlocks_Members_BlockerId", "FK_MemberBlocks_Members_BlockedId");
    }

    [Fact]
    public async Task Given_ABlock_When_InsertingTheSamePairAgain_Then_UniqueIndexRefusesItButReversePairIsAccepted()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertMembersAsync();
        await MigrateAsync(AddMemberBlocks);
        await InsertBlockAsync(_first, _second);

        // Act
        var duplicate = () => InsertBlockAsync(_first, _second);
        await InsertBlockAsync(_second, _first);

        // Assert
        await duplicate.Should().ThrowAsync<PostgresException>().Where(e => e.SqlState == PostgresErrorCodes.UniqueViolation);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "MemberBlocks" """)).Should().Be(2);
    }

    [Fact]
    public async Task Given_ASelfBlockRow_When_Inserting_Then_DatabaseAcceptsItBecauseTheRuleIsInTheDomain()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertMembersAsync();
        await MigrateAsync(AddMemberBlocks);

        // Act
        await InsertBlockAsync(_first, _first);

        // Assert
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "MemberBlocks" """)).Should().Be(1);
    }

    [Fact]
    public async Task Given_BlockWithUnknownMember_When_Inserting_Then_ForeignKeyRefusesIt()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertMembersAsync();
        await MigrateAsync(AddMemberBlocks);

        // Act
        var act = () => InsertBlockAsync(_first, Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<PostgresException>().Where(e => e.SqlState == PostgresErrorCodes.ForeignKeyViolation);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "MemberBlocks" """)).Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_Blocks_When_DeletingABlockerOrABlockedMember_Then_TheirBlocksAreDeleted(bool deleteBlocker)
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertMembersAsync();
        await MigrateAsync(AddMemberBlocks);
        await InsertBlockAsync(_first, _second);

        // Act
        await ExecuteAsync("""DELETE FROM "Members" WHERE "Id" = @id""", ("id", deleteBlocker ? _first : _second));

        // Assert
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "MemberBlocks" """)).Should().Be(0);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Members" """)).Should().Be(1);
    }

    [Fact]
    public async Task Given_Blocks_When_MigratingDownToPreviousMigration_Then_TableIsGoneAndMembersRemain()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertMembersAsync();
        await MigrateAsync(AddMemberBlocks);
        await InsertBlockAsync(_first, _second);

        // Act
        await MigrateAsync(PreviousMigration);

        // Assert
        (await TableExistsAsync("MemberBlocks")).Should().BeFalse();
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Members" """)).Should().Be(2);
    }

    private async Task MigrateAsync(string targetMigration)
    {
        var options = new DbContextOptionsBuilder<CampusMarketDbContext>().UseNpgsql(ConnectionString, npgsql => npgsql.UseVector()).Options;
        await using var db = new CampusMarketDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync(targetMigration);
    }

    private async Task InsertMembersAsync()
    {
        await InsertMemberAsync(_first, "Ion Ionescu", "ion@student.ro");
        await InsertMemberAsync(_second, "Ana Popescu", "ana@student.ro");
    }

    private async Task InsertMemberAsync(Guid id, string name, string email) =>
        await ExecuteAsync(
            """
            INSERT INTO "Members" ("Id", "Name", "Email", "CreatedAt")
            VALUES (@id, @name, @email, @createdAt)
            """,
            ("id", id), ("name", name), ("email", email), ("createdAt", T0));

    private async Task InsertBlockAsync(Guid blocker, Guid blocked) =>
        await ExecuteAsync(
            """
            INSERT INTO "MemberBlocks" ("Id", "BlockerId", "BlockedId", "CreatedAt")
            VALUES (@id, @blocker, @blocked, @createdAt)
            """,
            ("id", Guid.NewGuid()), ("blocker", blocker), ("blocked", blocked), ("createdAt", T0));

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
