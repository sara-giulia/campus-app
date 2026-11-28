using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusMarket.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261128150241_AddMemberReportsAndSuspension : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "SuspendedAt",
            table: "Members",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SuspendedById",
            table: "Members",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "SuspendedUntil",
            table: "Members",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SuspensionReason",
            table: "Members",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "MemberReports",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ReportedMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                ReporterId = table.Column<Guid>(type: "uuid", nullable: false),
                Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Resolution = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ResolvedById = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MemberReports", x => x.Id);
                table.ForeignKey(
                    name: "FK_MemberReports_Conversations_ConversationId",
                    column: x => x.ConversationId,
                    principalTable: "Conversations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_MemberReports_Members_ReportedMemberId",
                    column: x => x.ReportedMemberId,
                    principalTable: "Members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_MemberReports_Members_ReporterId",
                    column: x => x.ReporterId,
                    principalTable: "Members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MemberReports_ConversationId",
            table: "MemberReports",
            column: "ConversationId");

        migrationBuilder.CreateIndex(
            name: "IX_MemberReports_ReportedMemberId_Resolution",
            table: "MemberReports",
            columns: new[] { "ReportedMemberId", "Resolution" });

        migrationBuilder.CreateIndex(
            name: "IX_MemberReports_ReporterId_ReportedMemberId",
            table: "MemberReports",
            columns: new[] { "ReporterId", "ReportedMemberId" },
            unique: true,
            filter: "\"Resolution\" = 'Pending'");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MemberReports");

        migrationBuilder.DropColumn(
            name: "SuspendedAt",
            table: "Members");

        migrationBuilder.DropColumn(
            name: "SuspendedById",
            table: "Members");

        migrationBuilder.DropColumn(
            name: "SuspendedUntil",
            table: "Members");

        migrationBuilder.DropColumn(
            name: "SuspensionReason",
            table: "Members");
    }
}
