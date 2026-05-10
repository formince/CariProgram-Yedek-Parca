using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CariErinc.Migrations
{
    /// <inheritdoc />
    public partial class AddYedekParcaAlanlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AracMarkasi",
                table: "Urunler",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AracModeli",
                table: "Urunler",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModelYiliBaslangic",
                table: "Urunler",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModelYiliBitis",
                table: "Urunler",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotorTipi",
                table: "Urunler",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParcaTipi",
                table: "Urunler",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ParcaKodlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UrunId = table.Column<int>(type: "integer", nullable: false),
                    KodTipi = table.Column<int>(type: "integer", nullable: false),
                    Kod = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Aciklama = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OlusturulmaTarihi = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParcaKodlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParcaKodlari_Urunler_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "IsletmeAyarlar",
                keyColumn: "Id",
                keyValue: 1,
                column: "Deger",
                value: "Yedek Parça Dükkanı");

            migrationBuilder.UpdateData(
                table: "IsletmeAyarlar",
                keyColumn: "Id",
                keyValue: 2,
                column: "Deger",
                value: "YedekParca");

            migrationBuilder.CreateIndex(
                name: "IX_ParcaKodlari_Kod",
                table: "ParcaKodlari",
                column: "Kod");

            migrationBuilder.CreateIndex(
                name: "IX_ParcaKodlari_KodTipi_Kod",
                table: "ParcaKodlari",
                columns: new[] { "KodTipi", "Kod" });

            migrationBuilder.CreateIndex(
                name: "IX_ParcaKodlari_UrunId",
                table: "ParcaKodlari",
                column: "UrunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParcaKodlari");

            migrationBuilder.DropColumn(
                name: "AracMarkasi",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "AracModeli",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "ModelYiliBaslangic",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "ModelYiliBitis",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "MotorTipi",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "ParcaTipi",
                table: "Urunler");

            migrationBuilder.UpdateData(
                table: "IsletmeAyarlar",
                keyColumn: "Id",
                keyValue: 1,
                column: "Deger",
                value: "Kırtasiye Dükkanı");

            migrationBuilder.UpdateData(
                table: "IsletmeAyarlar",
                keyColumn: "Id",
                keyValue: 2,
                column: "Deger",
                value: "Kirtasiye");
        }
    }
}
