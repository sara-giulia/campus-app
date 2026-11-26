using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusMarket.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261125143000_AddListingExpiry : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ExpiresAt",
            table: "Listings",
            type: "timestamp with time zone",
            nullable: true);

        // Listings published before this migration get a full period from the migration time
        // (Listings:ExpiryDays defaults to 30), so nothing expires right after the deploy.
        // Reserved listings get it too: when their reservation expires or is cancelled they are Active
        // again, and an Active listing without ExpiresAt would never expire.
        // Drafts get it when they are published; Sold and Hidden listings never become Active again.
        migrationBuilder.Sql("""
            UPDATE "Listings"
            SET "ExpiresAt" = now() + interval '30 days'
            WHERE "Status" IN ('Active', 'Reserved');
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Expired listings go back to Active, the status they had before; the column then disappears.
        migrationBuilder.Sql("""
            UPDATE "Listings"
            SET "Status" = 'Active'
            WHERE "Status" = 'Expired';
            """);

        migrationBuilder.DropColumn(
            name: "ExpiresAt",
            table: "Listings");
    }
}
