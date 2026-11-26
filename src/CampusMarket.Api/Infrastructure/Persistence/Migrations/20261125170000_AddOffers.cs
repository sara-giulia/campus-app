using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusMarket.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261125170000_AddOffers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The price agreed through an accepted offer; existing reservations keep null (the listing's price).
        migrationBuilder.AddColumn<decimal>(
            name: "AgreedPrice",
            table: "Reservations",
            type: "numeric",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "Offers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                ProposedById = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric", nullable: false),
                Status = table.Column<string>(type: "text", nullable: false),
                ParentOfferId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Offers", x => x.Id);
                table.ForeignKey(
                    name: "FK_Offers_Listings_ListingId",
                    column: x => x.ListingId,
                    principalTable: "Listings",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Offers_Members_BuyerId",
                    column: x => x.BuyerId,
                    principalTable: "Members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Offers_Members_SellerId",
                    column: x => x.SellerId,
                    principalTable: "Members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Offers_Offers_ParentOfferId",
                    column: x => x.ParentOfferId,
                    principalTable: "Offers",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateIndex(
            name: "IX_Offers_BuyerId",
            table: "Offers",
            column: "BuyerId");

        migrationBuilder.CreateIndex(
            name: "IX_Offers_ListingId_BuyerId",
            table: "Offers",
            columns: new[] { "ListingId", "BuyerId" });

        migrationBuilder.CreateIndex(
            name: "IX_Offers_ListingId_Status",
            table: "Offers",
            columns: new[] { "ListingId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_Offers_ParentOfferId",
            table: "Offers",
            column: "ParentOfferId");

        migrationBuilder.CreateIndex(
            name: "IX_Offers_SellerId",
            table: "Offers",
            column: "SellerId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Offers");

        migrationBuilder.DropColumn(
            name: "AgreedPrice",
            table: "Reservations");
    }
}
