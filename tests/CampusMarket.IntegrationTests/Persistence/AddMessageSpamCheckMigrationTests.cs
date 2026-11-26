using CampusMarket.Api.Infrastructure.Persistence;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CampusMarket.IntegrationTests.Persistence;

// Runs the migrations by hand on a database of its own (never the one of a test host), so a message can be
// inserted between the migration before AddMessageSpamCheck and AddMessageSpamCheck itself.
public sealed class AddMessageSpamCheckMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261126112225_AddListingViews";
    private const string AddMessageSpamCheck = "20261126135244_AddMessageSpamCheck";

    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly string _database = $"campusmarket_migration_{Guid.NewGuid():N}";
    private readonly Guid _seller = Guid.NewGuid();
    private readonly Guid _buyer = Guid.NewGuid();
    private readonly Guid _listing = Guid.NewGuid();
    private readonly Guid _conversation = Guid.NewGuid();
    private readonly Guid _oldMessage = Guid.NewGuid();
    private string ConnectionString => SharedPostgres.ConnectionStringFor(_database);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        SharedPostgres.DropDatabase(_database);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Given_MessageBeforeTheMigration_When_MigratingToAddMessageSpamCheck_Then_ColumnsExistAndOldMessageIsNotSuspicious()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertConversationWithMessageAsync();

        // Act
        await MigrateAsync(AddMessageSpamCheck);

        // Assert
        var columns = await ColumnsAsync("Messages");
        columns["IsSuspicious"].Should().Be(("boolean", "NO"));
        columns["SpamProbability"].Should().Be(("double precision", "YES"));
        (await ScalarAsync<string>(
            "SELECT column_default FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'IsSuspicious'"))
            .Should().Be("false");

        (await ScalarAsync<bool>("""SELECT "IsSuspicious" FROM "Messages" WHERE "Id" = @id""", ("id", _oldMessage)))
            .Should().BeFalse();
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Messages" WHERE "Id" = @id AND "SpamProbability" IS NULL""", ("id", _oldMessage)))
            .Should().Be(1);
        (await ScalarAsync<string>("""SELECT "Text" FROM "Messages" WHERE "Id" = @id""", ("id", _oldMessage)))
            .Should().Be("Mai e disponibil?");
        (await ScalarAsync<DateTime>("""SELECT "SentAt" FROM "Messages" WHERE "Id" = @id""", ("id", _oldMessage)))
            .Should().Be(T0.UtcDateTime);
    }

    [Fact]
    public async Task Given_MigratedDatabase_When_InsertingMessageWithSpamCheck_Then_BothValuesAreSaved()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertConversationWithMessageAsync();
        await MigrateAsync(AddMessageSpamCheck);
        var newMessage = Guid.NewGuid();

        // Act
        await ExecuteAsync(
            """
            INSERT INTO "Messages" ("Id", "ConversationId", "Number", "SenderId", "Text", "SentAt", "IsSuspicious", "SpamProbability")
            VALUES (@id, @conversation, 2, @seller, 'Vezi www.casio.com', @sentAt, TRUE, 0.77)
            """,
            ("id", newMessage), ("conversation", _conversation), ("seller", _seller), ("sentAt", T0.AddMinutes(5)));

        // Assert
        (await ScalarAsync<bool>("""SELECT "IsSuspicious" FROM "Messages" WHERE "Id" = @id""", ("id", newMessage)))
            .Should().BeTrue();
        (await ScalarAsync<double>("""SELECT "SpamProbability" FROM "Messages" WHERE "Id" = @id""", ("id", newMessage)))
            .Should().Be(0.77);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Messages" """)).Should().Be(2);
    }

    [Fact]
    public async Task Given_MigratedDatabase_When_MigratingDownToAddListingViews_Then_ColumnsAreGoneAndMessageRemains()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertConversationWithMessageAsync();
        await MigrateAsync(AddMessageSpamCheck);

        // Act
        await MigrateAsync(PreviousMigration);

        // Assert
        var columns = await ColumnsAsync("Messages");
        columns.Should().NotContainKey("IsSuspicious").And.NotContainKey("SpamProbability");
        (await ScalarAsync<string>("""SELECT "Text" FROM "Messages" WHERE "Id" = @id""", ("id", _oldMessage)))
            .Should().Be("Mai e disponibil?");
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Messages" """)).Should().Be(1);
    }

    private async Task MigrateAsync(string targetMigration)
    {
        var options = new DbContextOptionsBuilder<CampusMarketDbContext>().UseNpgsql(ConnectionString, npgsql => npgsql.UseVector()).Options;
        await using var db = new CampusMarketDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync(targetMigration);
    }

    private async Task InsertConversationWithMessageAsync()
    {
        await InsertMemberAsync(_seller, "Ion Ionescu", "ion@student.ro");
        await InsertMemberAsync(_buyer, "Ana Popescu", "ana@student.ro");
        await ExecuteAsync(
            """
            INSERT INTO "Listings" ("Id", "SellerId", "Title", "Category", "Condition", "Price", "Status", "Version", "CreatedAt")
            VALUES (@id, @seller, 'Manual POO', 'Textbook', 'Good', 50, 'Active', 2, @createdAt)
            """,
            ("id", _listing), ("seller", _seller), ("createdAt", T0));
        await ExecuteAsync(
            """
            INSERT INTO "Conversations" ("Id", "ListingId", "SellerId", "BuyerId", "CreatedAt", "LastMessageAt")
            VALUES (@id, @listing, @seller, @buyer, @createdAt, @createdAt)
            """,
            ("id", _conversation), ("listing", _listing), ("seller", _seller), ("buyer", _buyer), ("createdAt", T0));
        await ExecuteAsync(
            """
            INSERT INTO "Messages" ("Id", "ConversationId", "Number", "SenderId", "Text", "SentAt")
            VALUES (@id, @conversation, 1, @buyer, 'Mai e disponibil?', @sentAt)
            """,
            ("id", _oldMessage), ("conversation", _conversation), ("buyer", _buyer), ("sentAt", T0));
    }

    private async Task InsertMemberAsync(Guid id, string name, string email) =>
        await ExecuteAsync(
            """
            INSERT INTO "Members" ("Id", "Name", "Email", "CreatedAt")
            VALUES (@id, @name, @email, @createdAt)
            """,
            ("id", id), ("name", name), ("email", email), ("createdAt", T0));

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
