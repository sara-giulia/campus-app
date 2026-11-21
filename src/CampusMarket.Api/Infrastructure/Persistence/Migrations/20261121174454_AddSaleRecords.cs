using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusMarket.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SaleRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ListingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Condition = table.Column<string>(type: "TEXT", nullable: false),
                    SuggestedPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    SoldPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    SoldAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleRecords_SoldAt",
                table: "SaleRecords",
                column: "SoldAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaleRecords");
        }
    }
}
