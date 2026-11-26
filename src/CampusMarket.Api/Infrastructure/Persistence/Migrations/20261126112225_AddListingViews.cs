using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusMarket.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261126112225_AddListingViews : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ListingViews",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                ViewerKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Day = table.Column<DateOnly>(type: "date", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ListingViews", x => x.Id);
                table.ForeignKey(
                    name: "FK_ListingViews_Listings_ListingId",
                    column: x => x.ListingId,
                    principalTable: "Listings",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ListingViews_ListingId_ViewerKey_Day",
            table: "ListingViews",
            columns: new[] { "ListingId", "ViewerKey", "Day" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ListingViews");
    }
}
