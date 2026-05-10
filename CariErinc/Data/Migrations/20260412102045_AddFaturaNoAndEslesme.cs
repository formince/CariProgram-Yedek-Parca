using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CariErinc.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFaturaNoAndEslesme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FaturaNo",
                table: "Alislar",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FaturaEslesmeleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TedarikciId = table.Column<int>(type: "integer", nullable: false),
                    FaturaUrunAdi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SistemUrunId = table.Column<int>(type: "integer", nullable: false),
                    KullaniciId = table.Column<int>(type: "integer", nullable: false),
                    EslesmeSkoru = table.Column<int>(type: "integer", nullable: false),
                    ManuelMi = table.Column<bool>(type: "boolean", nullable: false),
                    KayitTarihi = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaturaEslesmeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaturaEslesmeleri_Kullanicilar_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FaturaEslesmeleri_Tedarikciler_TedarikciId",
                        column: x => x.TedarikciId,
                        principalTable: "Tedarikciler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FaturaEslesmeleri_Urunler_SistemUrunId",
                        column: x => x.SistemUrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FaturaEslesmeleri_KullaniciId",
                table: "FaturaEslesmeleri",
                column: "KullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_FaturaEslesmeleri_SistemUrunId",
                table: "FaturaEslesmeleri",
                column: "SistemUrunId");

            migrationBuilder.CreateIndex(
                name: "IX_FaturaEslesmeleri_TedarikciId",
                table: "FaturaEslesmeleri",
                column: "TedarikciId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FaturaEslesmeleri");

            migrationBuilder.DropColumn(
                name: "FaturaNo",
                table: "Alislar");
        }
    }
}
