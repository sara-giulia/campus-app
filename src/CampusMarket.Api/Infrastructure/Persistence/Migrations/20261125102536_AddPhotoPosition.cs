using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusMarket.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261125102536_AddPhotoPosition : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Position",
            table: "Photos",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        // Existing photos keep the order they had (CreatedAt, then Id), numbered from 0 in each listing.
        migrationBuilder.Sql("""
            UPDATE "Photos" AS p
            SET "Position" = ranked."Position"
            FROM (
                SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "ListingId" ORDER BY "CreatedAt", "Id") - 1 AS "Position"
                FROM "Photos"
            ) AS ranked
            WHERE p."Id" = ranked."Id";
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Position",
            table: "Photos");
    }
}
