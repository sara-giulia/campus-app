using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusMarket.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261126192341_AddConversationReadState : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "BuyerLastReadNumber",
            table: "Conversations",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "SellerLastReadNumber",
            table: "Conversations",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        // The conversations from before this migration count as read on both sides, so nobody finds
        // every old message marked as new after the deploy.
        migrationBuilder.Sql("""
            UPDATE "Conversations" AS c
            SET "BuyerLastReadNumber" = m."Last", "SellerLastReadNumber" = m."Last"
            FROM (SELECT "ConversationId", MAX("Number") AS "Last" FROM "Messages" GROUP BY "ConversationId") AS m
            WHERE m."ConversationId" = c."Id";
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BuyerLastReadNumber",
            table: "Conversations");

        migrationBuilder.DropColumn(
            name: "SellerLastReadNumber",
            table: "Conversations");
    }
}
