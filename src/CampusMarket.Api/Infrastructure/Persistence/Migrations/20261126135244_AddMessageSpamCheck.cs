using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusMarket.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261126135244_AddMessageSpamCheck : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsSuspicious",
            table: "Messages",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<double>(
            name: "SpamProbability",
            table: "Messages",
            type: "double precision",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsSuspicious",
            table: "Messages");

        migrationBuilder.DropColumn(
            name: "SpamProbability",
            table: "Messages");
    }
}
