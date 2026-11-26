using CampusMarket.Api.Infrastructure.Persistence;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CampusMarket.IntegrationTests.Persistence;

// Runs the migrations by hand on a database of its own (never the one of a test host), so conversations can be
// inserted between the migration before AddConversationReadState and AddConversationReadState itself.
public sealed class AddConversationReadStateMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261126150921_AddTrainingExamples";
    private const string AddConversationReadState = "20261126192341_AddConversationReadState";

    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly string _database = $"campusmarket_migration_{Guid.NewGuid():N}";
    private readonly Guid _seller = Guid.NewGuid();
    private readonly Guid _firstBuyer = Guid.NewGuid();
    private readonly Guid _secondBuyer = Guid.NewGuid();
    private readonly Guid _listing = Guid.NewGuid();
    private readonly Guid _longConversation = Guid.NewGuid();
    private readonly Guid _shortConversation = Guid.NewGuid();
    private string ConnectionString => SharedPostgres.ConnectionStringFor(_database);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        SharedPostgres.DropDatabase(_database);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Given_ConversationsBeforeTheMigration_When_MigratingToAddConversationReadState_Then_ColumnsExistAndBothSidesHaveReadEverything()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertConversationsAsync();

        // Act
        await MigrateAsync(AddConversationReadState);

        // Assert
        var columns = await ColumnsAsync("Conversations");
        columns["BuyerLastReadNumber"].Should().Be(("integer", "NO"));
        columns["SellerLastReadNumber"].Should().Be(("integer", "NO"));

        (await MarksAsync(_longConversation)).Should().Be((3, 3));
        (await MarksAsync(_shortConversation)).Should().Be((1, 1));
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Messages" """)).Should().Be(4);
    }

    [Fact]
    public async Task Given_MigratedDatabase_When_InsertingConversationWithoutTheColumns_Then_BothMarksAreZero()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertConversationsAsync();
        await MigrateAsync(AddConversationReadState);
        var otherListing = Guid.NewGuid();
        await InsertListingAsync(otherListing);
        var newConversation = Guid.NewGuid();

        // Act
        await InsertConversationAsync(newConversation, _secondBuyer, otherListing);

        // Assert
        (await MarksAsync(newConversation)).Should().Be((0, 0));
        (await MarksAsync(_longConversation)).Should().Be((3, 3));
    }

    [Fact]
    public async Task Given_MigratedDatabase_When_MigratingDownToAddTrainingExamples_Then_ColumnsAreGoneAndDataRemains()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertConversationsAsync();
        await MigrateAsync(AddConversationReadState);

        // Act
        await MigrateAsync(PreviousMigration);

        // Assert
        var columns = await ColumnsAsync("Conversations");
        columns.Should().NotContainKey("BuyerLastReadNumber").And.NotContainKey("SellerLastReadNumber");
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Conversations" """)).Should().Be(2);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Messages" WHERE "ConversationId" = @id""", ("id", _longConversation)))
            .Should().Be(3);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Messages" WHERE "ConversationId" = @id""", ("id", _shortConversation)))
            .Should().Be(1);
    }

    private async Task MigrateAsync(string targetMigration)
    {
        var options = new DbContextOptionsBuilder<CampusMarketDbContext>().UseNpgsql(ConnectionString, npgsql => npgsql.UseVector()).Options;
        await using var db = new CampusMarketDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync(targetMigration);
    }

    // Three members, a listing, a conversation with 3 messages (buyer, seller, buyer) and one with 1 message.
    private async Task InsertConversationsAsync()
    {
        await InsertMemberAsync(_seller, "Ion Ionescu", "ion@student.ro");
        await InsertMemberAsync(_firstBuyer, "Ana Popescu", "ana@student.ro");
        await InsertMemberAsync(_secondBuyer, "Mihai Popa", "mihai@student.ro");
        await InsertListingAsync(_listing);

        await InsertConversationAsync(_longConversation, _firstBuyer);
        await InsertMessageAsync(_longConversation, 1, _firstBuyer, "Mai e disponibil?");
        await InsertMessageAsync(_longConversation, 2, _seller, "Da, inca e.");
        await InsertMessageAsync(_longConversation, 3, _firstBuyer, "Pot sa vin maine?");

        await InsertConversationAsync(_shortConversation, _secondBuyer);
        await InsertMessageAsync(_shortConversation, 1, _secondBuyer, "Il mai vinzi?");
    }

    private async Task InsertMemberAsync(Guid id, string name, string email) =>
        await ExecuteAsync(
            """
            INSERT INTO "Members" ("Id", "Name", "Email", "CreatedAt")
            VALUES (@id, @name, @email, @createdAt)
            """,
            ("id", id), ("name", name), ("email", email), ("createdAt", T0));

    private async Task InsertListingAsync(Guid id) =>
        await ExecuteAsync(
            """
            INSERT INTO "Listings" ("Id", "SellerId", "Title", "Category", "Condition", "Price", "Status", "Version", "CreatedAt")
            VALUES (@id, @seller, 'Manual POO', 'Textbook', 'Good', 50, 'Active', 2, @createdAt)
            """,
            ("id", id), ("seller", _seller), ("createdAt", T0));

    // A conversation about the listing (the class one when none is given), without the read columns.
    private async Task InsertConversationAsync(Guid id, Guid buyer, Guid? listing = null) =>
        await ExecuteAsync(
            """
            INSERT INTO "Conversations" ("Id", "ListingId", "SellerId", "BuyerId", "CreatedAt", "LastMessageAt")
            VALUES (@id, @listing, @seller, @buyer, @createdAt, @createdAt)
            """,
            ("id", id), ("listing", listing ?? _listing), ("seller", _seller), ("buyer", buyer), ("createdAt", T0));

    private async Task InsertMessageAsync(Guid conversation, int number, Guid sender, string text) =>
        await ExecuteAsync(
            """
            INSERT INTO "Messages" ("Id", "ConversationId", "Number", "SenderId", "Text", "SentAt")
            VALUES (@id, @conversation, @number, @sender, @text, @sentAt)
            """,
            ("id", Guid.NewGuid()), ("conversation", conversation), ("number", number), ("sender", sender),
            ("text", text), ("sentAt", T0.AddMinutes(number)));

    private async Task<(int Buyer, int Seller)> MarksAsync(Guid conversation)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """SELECT "BuyerLastReadNumber", "SellerLastReadNumber" FROM "Conversations" WHERE "Id" = @id""", connection);
        command.Parameters.AddWithValue("id", conversation);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        return (reader.GetInt32(0), reader.GetInt32(1));
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
