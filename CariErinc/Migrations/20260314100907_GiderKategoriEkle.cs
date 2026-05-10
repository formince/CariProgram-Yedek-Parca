using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CariErinc.Migrations
{
    /// <inheritdoc />
    public partial class GiderKategoriEkle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GiderKategoriId",
                table: "KasaHareketler",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GiderKategoriler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tip = table.Column<int>(type: "integer", nullable: false),
                    SilinebilirMi = table.Column<bool>(type: "boolean", nullable: false),
                    AktifMi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiderKategoriler", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "GiderKategoriler",
                columns: new[] { "Id", "Ad", "AktifMi", "SilinebilirMi", "Tip" },
                values: new object[,]
                {
                    { 1, "Satış", true, false, 0 },
                    { 2, "Veresiye Ödeme", true, false, 0 },
                    { 3, "Alış", true, false, 1 },
                    { 4, "Kira", true, true, 1 },
                    { 5, "Fatura", true, true, 1 },
                    { 6, "Maaş", true, true, 1 },
                    { 7, "Diğer", true, true, 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_KasaHareketler_GiderKategoriId",
                table: "KasaHareketler",
                column: "GiderKategoriId");

            migrationBuilder.AddForeignKey(
                name: "FK_KasaHareketler_GiderKategoriler_GiderKategoriId",
                table: "KasaHareketler",
                column: "GiderKategoriId",
                principalTable: "GiderKategoriler",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KasaHareketler_GiderKategoriler_GiderKategoriId",
                table: "KasaHareketler");

            migrationBuilder.DropTable(
                name: "GiderKategoriler");

            migrationBuilder.DropIndex(
                name: "IX_KasaHareketler_GiderKategoriId",
                table: "KasaHareketler");

            migrationBuilder.DropColumn(
                name: "GiderKategoriId",
                table: "KasaHareketler");
        }
    }
}
