using CampusMarket.Api.Infrastructure.Persistence;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CampusMarket.IntegrationTests.Persistence;

// Runs the migrations by hand on a database of its own (never the one of a test host), so the data can be
// inserted between the migration before AddOffers and AddOffers itself.
public sealed class AddOffersMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261125143000_AddListingExpiry";
    private const string AddOffers = "20261125170000_AddOffers";

    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly string _database = $"campusmarket_migration_{Guid.NewGuid():N}";
    private readonly Guid _seller = Guid.NewGuid();
    private readonly Guid _buyer = Guid.NewGuid();
    private readonly Guid _listing = Guid.NewGuid();
    private readonly Guid _reservation = Guid.NewGuid();
    private string ConnectionString => SharedPostgres.ConnectionStringFor(_database);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        SharedPostgres.DropDatabase(_database);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Given_ReservationBeforeTheMigration_When_MigratingToAddOffers_Then_ReservationKeepsNullAgreedPrice()
    {
        // Arrange: the schema before AddOffers, with a seller, a buyer, a listing and a reservation.
        await MigrateAsync(PreviousMigration);
        await InsertMemberAndListingAndReservationAsync();
        (await ColumnExistsAsync("Reservations", "AgreedPrice")).Should().BeFalse();
        (await TableExistsAsync("Offers")).Should().BeFalse();

        // Act
        await MigrateAsync(AddOffers);

        // Assert
        (await ColumnExistsAsync("Reservations", "AgreedPrice")).Should().BeTrue();
        var reservation = await ReadReservationAsync();
        reservation.Id.Should().Be(_reservation);
        reservation.ListingId.Should().Be(_listing);
        reservation.MemberId.Should().Be(_buyer);
        reservation.Status.Should().Be("Active");
        reservation.AgreedPrice.Should().BeNull();
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Listings" """)).Should().Be(1);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Members" """)).Should().Be(2);
    }

    [Fact]
    public async Task Given_MigrationToAddOffers_When_ReadingTheSchema_Then_OffersTableAndIndexesExist()
    {
        // Act
        await MigrateAsync(AddOffers);

        // Assert
        (await TableExistsAsync("Offers")).Should().BeTrue();
        var columns = await ColumnsAsync("Offers");
        columns.Keys.Should().BeEquivalentTo(
            "Id", "ListingId", "BuyerId", "SellerId", "ProposedById", "Amount", "Status", "ParentOfferId", "CreatedAt", "RespondedAt");
        columns["Status"].Should().Be("text");
        columns["Amount"].Should().Be("numeric");
        columns["ParentOfferId"].Should().Be("uuid");
        (await ColumnsAsync("Reservations"))["AgreedPrice"].Should().Be("numeric");

        var indexes = await ScalarListAsync("SELECT indexname FROM pg_indexes WHERE tablename = 'Offers'");
        indexes.Should().Contain([
            "IX_Offers_BuyerId",
            "IX_Offers_ListingId_BuyerId",
            "IX_Offers_ListingId_Status",
            "IX_Offers_ParentOfferId",
            "IX_Offers_SellerId"
        ]);
    }

    [Fact]
    public async Task Given_AddOffersMigration_When_DeletingTheListing_Then_ItsOffersAreDeleted()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertMemberAndListingAndReservationAsync();
        await MigrateAsync(AddOffers);
        var offer = Guid.NewGuid();
        await InsertOfferAsync(offer);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Offers" """)).Should().Be(1);

        // Act
        await ExecuteAsync("""DELETE FROM "Listings" WHERE "Id" = @id""", ("id", _listing));

        // Assert
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Offers" """)).Should().Be(0);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Members" """)).Should().Be(2);
    }

    [Fact]
    public async Task Given_AddOffersMigration_When_DeletingTheBuyer_Then_TheirOffersAreDeleted()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertMemberAndListingAndReservationAsync();
        await MigrateAsync(AddOffers);
        await InsertOfferAsync(Guid.NewGuid());

        // Act
        await ExecuteAsync("""DELETE FROM "Members" WHERE "Id" = @id""", ("id", _buyer));

        // Assert
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Offers" """)).Should().Be(0);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Listings" """)).Should().Be(1);
    }

    [Fact]
    public async Task Given_OfferWithUnknownMember_When_Inserting_Then_ForeignKeyRefusesIt()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertMemberAndListingAndReservationAsync();
        await MigrateAsync(AddOffers);

        // Act
        var act = () => ExecuteAsync(
            """
            INSERT INTO "Offers" ("Id", "ListingId", "BuyerId", "SellerId", "ProposedById", "Amount", "Status", "CreatedAt")
            VALUES (@id, @listing, @buyer, @seller, @buyer, 90, 'Pending', @createdAt)
            """,
            ("id", Guid.NewGuid()), ("listing", _listing), ("buyer", Guid.NewGuid()), ("seller", _seller), ("createdAt", T0));

        // Assert
        await act.Should().ThrowAsync<PostgresException>().Where(e => e.SqlState == PostgresErrorCodes.ForeignKeyViolation);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Offers" """)).Should().Be(0);
    }

    [Fact]
    public async Task Given_OffersAndAgreedPrice_When_MigratingDownToPreviousMigration_Then_TableAndColumnAreGoneAndReservationRemains()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertMemberAndListingAndReservationAsync();
        await MigrateAsync(AddOffers);
        await InsertOfferAsync(Guid.NewGuid());
        await ExecuteAsync("""UPDATE "Reservations" SET "AgreedPrice" = 90 WHERE "Id" = @id""", ("id", _reservation));

        // Act
        await MigrateAsync(PreviousMigration);

        // Assert
        (await TableExistsAsync("Offers")).Should().BeFalse();
        (await ColumnExistsAsync("Reservations", "AgreedPrice")).Should().BeFalse();
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Reservations" WHERE "Id" = @id""", ("id", _reservation))).Should().Be(1);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Listings" """)).Should().Be(1);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Members" """)).Should().Be(2);
    }

    private async Task MigrateAsync(string targetMigration)
    {
        var options = new DbContextOptionsBuilder<CampusMarketDbContext>().UseNpgsql(ConnectionString, npgsql => npgsql.UseVector()).Options;
        await using var db = new CampusMarketDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync(targetMigration);
    }

    // A seller and a buyer (real members, the offers need them), one Active listing and an Active reservation of the buyer.
    private async Task InsertMemberAndListingAndReservationAsync()
    {
        await InsertMemberAsync(_seller, "Ion Ionescu", "ion@student.ro");
        await InsertMemberAsync(_buyer, "Ana Popescu", "ana@student.ro");
        await ExecuteAsync(
            """
            INSERT INTO "Listings" ("Id", "SellerId", "Title", "Category", "Condition", "Price", "Status", "Version", "CreatedAt")
            VALUES (@id, @seller, 'Manual POO', 'Textbook', 'Good', 100, 'Reserved', 2, @createdAt)
            """,
            ("id", _listing), ("seller", _seller), ("createdAt", T0));
        await ExecuteAsync(
            """
            INSERT INTO "Reservations" ("Id", "ListingId", "MemberId", "CreatedAt", "ExpiresAt", "Status")
            VALUES (@id, @listing, @buyer, @createdAt, @expiresAt, 'Active')
            """,
            ("id", _reservation), ("listing", _listing), ("buyer", _buyer), ("createdAt", T0), ("expiresAt", T0.AddHours(48)));
    }

    private async Task InsertMemberAsync(Guid id, string name, string email) =>
        await ExecuteAsync(
            """
            INSERT INTO "Members" ("Id", "Name", "Email", "CreatedAt")
            VALUES (@id, @name, @email, @createdAt)
            """,
            ("id", id), ("name", name), ("email", email), ("createdAt", T0));

    private async Task InsertOfferAsync(Guid id) =>
        await ExecuteAsync(
            """
            INSERT INTO "Offers" ("Id", "ListingId", "BuyerId", "SellerId", "ProposedById", "Amount", "Status", "CreatedAt")
            VALUES (@id, @listing, @buyer, @seller, @buyer, 90, 'Pending', @createdAt)
            """,
            ("id", id), ("listing", _listing), ("buyer", _buyer), ("seller", _seller), ("createdAt", T0));

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

    private async Task<bool> ColumnExistsAsync(string table, string column) =>
        await ScalarAsync<long>(
            "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = @t AND column_name = @c",
            ("t", table), ("c", column)) > 0;

    // Column name -> data type.
    private async Task<Dictionary<string, string>> ColumnsAsync(string table)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT column_name, data_type FROM information_schema.columns WHERE table_name = @t", connection);
        command.Parameters.AddWithValue("t", table);
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new Dictionary<string, string>();
        while (await reader.ReadAsync())
            columns[reader.GetString(0)] = reader.GetString(1);
        return columns;
    }

    private async Task<(Guid Id, Guid ListingId, Guid MemberId, string Status, decimal? AgreedPrice)> ReadReservationAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """SELECT "Id", "ListingId", "MemberId", "Status", "AgreedPrice" FROM "Reservations" """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        return (reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetDecimal(4));
    }
}
