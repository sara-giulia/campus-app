using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusMarket.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddGeneratedPhotos : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsGenerated",
            table: "Photos",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsGenerated",
            table: "Photos");
    }
}
