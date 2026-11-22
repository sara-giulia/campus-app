using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CampusMarket.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Key);
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Key", "Description", "DisplayName", "IsActive" },
                values: new object[,]
                {
                    { "Electronics", null, "Electronice", true },
                    { "LabEquipment", null, "Echipament de laborator", true },
                    { "Notes", null, "Notițe", true },
                    { "Other", null, "Altele", true },
                    { "Textbook", null, "Manuale", true }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
