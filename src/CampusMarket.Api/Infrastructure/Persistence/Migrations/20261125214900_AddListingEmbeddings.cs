using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace CampusMarket.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261125214900_AddListingEmbeddings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:PostgresExtension:vector", ",,");

        migrationBuilder.CreateTable(
            name: "ListingEmbeddings",
            columns: table => new
            {
                ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                Embedding = table.Column<Vector>(type: "vector(768)", nullable: false),
                Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                TextHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                IndexedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ListingEmbeddings", x => x.ListingId);
                table.ForeignKey(
                    name: "FK_ListingEmbeddings_Listings_ListingId",
                    column: x => x.ListingId,
                    principalTable: "Listings",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ListingEmbeddings_Embedding",
            table: "ListingEmbeddings",
            column: "Embedding")
            .Annotation("Npgsql:IndexMethod", "hnsw")
            .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ListingEmbeddings");

        migrationBuilder.AlterDatabase()
            .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
    }
}
