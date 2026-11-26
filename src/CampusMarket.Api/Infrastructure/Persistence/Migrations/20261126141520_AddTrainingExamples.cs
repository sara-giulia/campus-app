using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusMarket.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261126141520_AddTrainingExamples : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Resolution",
            table: "Reports",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Pending");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ResolvedAt",
            table: "Reports",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "TrainingExamples",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                Label = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TrainingExamples", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Reports_ListingId_Resolution",
            table: "Reports",
            columns: new[] { "ListingId", "Resolution" });

        migrationBuilder.CreateIndex(
            name: "IX_TrainingExamples_CreatedAt",
            table: "TrainingExamples",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_TrainingExamples_ListingId_Kind",
            table: "TrainingExamples",
            columns: new[] { "ListingId", "Kind" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TrainingExamples");

        migrationBuilder.DropIndex(
            name: "IX_Reports_ListingId_Resolution",
            table: "Reports");

        migrationBuilder.DropColumn(
            name: "Resolution",
            table: "Reports");

        migrationBuilder.DropColumn(
            name: "ResolvedAt",
            table: "Reports");
    }
}
