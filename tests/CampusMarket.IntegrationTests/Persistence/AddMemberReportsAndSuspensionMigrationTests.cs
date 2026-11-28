using CampusMarket.Api.Infrastructure.Persistence;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CampusMarket.IntegrationTests.Persistence;

// Runs the migrations by hand on a database of its own (never the one of a test host), so members can be
// inserted between the migration before AddMemberReportsAndSuspension and AddMemberReportsAndSuspension itself.
public sealed class AddMemberReportsAndSuspensionMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261127134622_AddPhotoThumbnails";
    private const string AddMemberReportsAndSuspension = "20261128150241_AddMemberReportsAndSuspension";
    private const string UniqueViolation = "23505";

    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly string _database = $"campusmarket_migration_{Guid.NewGuid():N}";
    private readonly Guid _ion = Guid.NewGuid();
    private readonly Guid _ana = Guid.NewGuid();
    private readonly Guid _listing = Guid.NewGuid();
    private readonly Guid _conversation = Guid.NewGuid();
    private string ConnectionString => SharedPostgres.ConnectionStringFor(_database);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        SharedPostgres.DropDatabase(_database);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Given_MembersBeforeTheMigration_When_MigratingToAddMemberReportsAndSuspension_Then_TheSuspensionColumnsExistAndAreNull()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertMembersAsync();

        // Act
        await MigrateAsync(AddMemberReportsAndSuspension);

        // Assert
        var columns = await ColumnsAsync("Members");
        columns["SuspendedAt"].Should().Be(("timestamp with time zone", "YES"));
        columns["SuspendedUntil"].Should().Be(("timestamp with time zone", "YES"));
        columns["SuspendedById"].Should().Be(("uuid", "YES"));
        columns["SuspensionReason"].Should().Be(("character varying", "YES"));
        (await ScalarAsync<int>(
            """SELECT character_maximum_length FROM information_schema.columns WHERE table_name = 'Members' AND column_name = 'SuspensionReason'"""))
            .Should().Be(500);

        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Members" """)).Should().Be(2);
        (await ScalarAsync<long>(
            """
            SELECT COUNT(*) FROM "Members"
            WHERE "SuspendedAt" IS NULL AND "SuspendedUntil" IS NULL AND "SuspensionReason" IS NULL AND "SuspendedById" IS NULL
            """)).Should().Be(2);

        var reportColumns = await ColumnsAsync("MemberReports");
        reportColumns.Keys.Should().BeEquivalentTo(
            "Id", "ReportedMemberId", "ReporterId", "Reason", "ConversationId", "CreatedAt", "Resolution", "ResolvedAt", "ResolvedById");
        (await ScalarAsync<string>(
            """SELECT indexdef FROM pg_indexes WHERE indexname = 'IX_MemberReports_ReporterId_ReportedMemberId'"""))
            .Should().Contain("UNIQUE").And.Contain("WHERE").And.Contain("'Pending'");
    }

    [Fact]
    public async Task Given_PendingReport_When_InsertingASecondPendingReportOfTheSamePair_Then_TheUniqueIndexRefusesIt()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertMembersAsync();
        await MigrateAsync(AddMemberReportsAndSuspension);
        await InsertReportAsync(Guid.NewGuid(), _ana, _ion);

        // Act
        var act = () => InsertReportAsync(Guid.NewGuid(), _ana, _ion);

        // Assert
        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(UniqueViolation);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "MemberReports" """)).Should().Be(1);
    }

    [Fact]
    public async Task Given_ResolvedReport_When_InsertingANewPendingReportOfTheSamePair_Then_ItIsAccepted()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertMembersAsync();
        await MigrateAsync(AddMemberReportsAndSuspension);
        var first = Guid.NewGuid();
        await InsertReportAsync(first, _ana, _ion);
        await ExecuteAsync("""UPDATE "MemberReports" SET "Resolution" = 'Dismissed', "ResolvedAt" = @t WHERE "Id" = @id""",
            ("t", T0.AddHours(1)), ("id", first));

        // Act
        await InsertReportAsync(Guid.NewGuid(), _ana, _ion);

        // Assert
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "MemberReports" WHERE "Resolution" = 'Pending'""")).Should().Be(1);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "MemberReports" WHERE "Resolution" = 'Dismissed'""")).Should().Be(1);
    }

    [Fact]
    public async Task Given_ReportWithoutResolution_When_Inserting_Then_ItIsPendingByDefault()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertMembersAsync();
        await MigrateAsync(AddMemberReportsAndSuspension);
        var id = Guid.NewGuid();

        // Act
        await InsertReportAsync(id, _ana, _ion);

        // Assert
        (await ScalarAsync<string>("""SELECT "Resolution" FROM "MemberReports" WHERE "Id" = @id""", ("id", id))).Should().Be("Pending");
    }

    [Fact]
    public async Task Given_ReportWithAConversation_When_TheConversationIsDeleted_Then_TheReportStaysWithoutIt()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertMembersAsync();
        await InsertConversationAsync();
        await MigrateAsync(AddMemberReportsAndSuspension);
        var id = Guid.NewGuid();
        await InsertReportAsync(id, _ana, _ion, _conversation);

        // Act
        await ExecuteAsync("""DELETE FROM "Conversations" WHERE "Id" = @id""", ("id", _conversation));

        // Assert
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "MemberReports" WHERE "Id" = @id AND "ConversationId" IS NULL""", ("id", id)))
            .Should().Be(1);
    }

    [Fact]
    public async Task Given_ReportOnAMember_When_TheMemberIsDeleted_Then_TheReportIsDeleted()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertMembersAsync();
        await MigrateAsync(AddMemberReportsAndSuspension);
        await InsertReportAsync(Guid.NewGuid(), _ana, _ion);

        // Act
        await ExecuteAsync("""DELETE FROM "Members" WHERE "Id" = @id""", ("id", _ion));

        // Assert
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "MemberReports" """)).Should().Be(0);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "Members" """)).Should().Be(1);
    }

    [Fact]
    public async Task Given_MigratedDatabase_When_MigratingDownToAddPhotoThumbnails_Then_TableAndColumnsAreGoneAndMembersRemain()
    {
        // Arrange
        await MigrateAsync(PreviousMigration);
        await InsertMembersAsync();
        await MigrateAsync(AddMemberReportsAndSuspension);
        await InsertReportAsync(Guid.NewGuid(), _ana, _ion);

        // Act
        await MigrateAsync(PreviousMigration);

        // Assert
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'MemberReports'""")).Should().Be(0);
        var columns = await ColumnsAsync("Members");
        columns.Should().NotContainKey("SuspendedAt").And.NotContainKey("SuspendedUntil")
            .And.NotContainKey("SuspensionReason").And.NotContainKey("SuspendedById");
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
        await InsertMemberAsync(_ion, "Ion Ionescu", "ion@student.ro");
        await InsertMemberAsync(_ana, "Ana Popescu", "ana@student.ro");
    }

    private async Task InsertMemberAsync(Guid id, string name, string email) =>
        await ExecuteAsync(
            """
            INSERT INTO "Members" ("Id", "Name", "Email", "CreatedAt")
            VALUES (@id, @name, @email, @createdAt)
            """,
            ("id", id), ("name", name), ("email", email), ("createdAt", T0));

    // A listing of Ion and Ana's conversation about it.
    private async Task InsertConversationAsync()
    {
        await ExecuteAsync(
            """
            INSERT INTO "Listings" ("Id", "SellerId", "Title", "Category", "Condition", "Price", "Status", "Version", "CreatedAt")
            VALUES (@id, @seller, 'Manual POO', 'Textbook', 'Good', 50, 'Active', 2, @createdAt)
            """,
            ("id", _listing), ("seller", _ion), ("createdAt", T0));
        await ExecuteAsync(
            """
            INSERT INTO "Conversations" ("Id", "ListingId", "SellerId", "BuyerId", "CreatedAt", "LastMessageAt")
            VALUES (@id, @listing, @seller, @buyer, @createdAt, @createdAt)
            """,
            ("id", _conversation), ("listing", _listing), ("seller", _ion), ("buyer", _ana), ("createdAt", T0));
    }

    // A report without Resolution, so the column default applies.
    private async Task InsertReportAsync(Guid id, Guid reporter, Guid reported, Guid? conversation = null) =>
        await ExecuteAsync(
            """
            INSERT INTO "MemberReports" ("Id", "ReportedMemberId", "ReporterId", "Reason", "ConversationId", "CreatedAt")
            VALUES (@id, @reported, @reporter, 'Cere bani in avans', @conversation, @createdAt)
            """,
            ("id", id), ("reported", reported), ("reporter", reporter),
            ("conversation", conversation is { } c ? c : DBNull.Value), ("createdAt", T0));

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
