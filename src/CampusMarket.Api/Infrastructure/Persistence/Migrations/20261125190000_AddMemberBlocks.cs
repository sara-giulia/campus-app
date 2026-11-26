using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusMarket.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261125190000_AddMemberBlocks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MemberBlocks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BlockerId = table.Column<Guid>(type: "uuid", nullable: false),
                BlockedId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MemberBlocks", x => x.Id);
                table.ForeignKey(
                    name: "FK_MemberBlocks_Members_BlockedId",
                    column: x => x.BlockedId,
                    principalTable: "Members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_MemberBlocks_Members_BlockerId",
                    column: x => x.BlockerId,
                    principalTable: "Members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MemberBlocks_BlockedId",
            table: "MemberBlocks",
            column: "BlockedId");

        migrationBuilder.CreateIndex(
            name: "IX_MemberBlocks_BlockerId_BlockedId",
            table: "MemberBlocks",
            columns: new[] { "BlockerId", "BlockedId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MemberBlocks");
    }
}
