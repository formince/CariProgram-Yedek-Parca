using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CariErinc.Migrations
{
    /// <inheritdoc />
    public partial class IadeIptalEkle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IptalEdildi",
                table: "Satislar",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IptalNedeni",
                table: "Satislar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IptalTarihi",
                table: "Satislar",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "KismiIade",
                table: "Satislar",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SatisIadeler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SatisId = table.Column<int>(type: "integer", nullable: false),
                    IadeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Neden = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatisIadeler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SatisIadeler_Satislar_SatisId",
                        column: x => x.SatisId,
                        principalTable: "Satislar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SatisIadeDetaylari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SatisIadeId = table.Column<int>(type: "integer", nullable: false),
                    SatisDetayId = table.Column<int>(type: "integer", nullable: false),
                    IadeMiktar = table.Column<int>(type: "integer", nullable: false),
                    IadeTutari = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatisIadeDetaylari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SatisIadeDetaylari_SatisDetaylari_SatisDetayId",
                        column: x => x.SatisDetayId,
                        principalTable: "SatisDetaylari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SatisIadeDetaylari_SatisIadeler_SatisIadeId",
                        column: x => x.SatisIadeId,
                        principalTable: "SatisIadeler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "GiderKategoriler",
                columns: new[] { "Id", "Ad", "AktifMi", "SilinebilirMi", "Tip" },
                values: new object[,]
                {
                    { 8, "Satış İadesi", true, false, 1 },
                    { 9, "Alış İadesi", true, false, 0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SatisIadeDetaylari_SatisDetayId",
                table: "SatisIadeDetaylari",
                column: "SatisDetayId");

            migrationBuilder.CreateIndex(
                name: "IX_SatisIadeDetaylari_SatisIadeId",
                table: "SatisIadeDetaylari",
                column: "SatisIadeId");

            migrationBuilder.CreateIndex(
                name: "IX_SatisIadeler_SatisId",
                table: "SatisIadeler",
                column: "SatisId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SatisIadeDetaylari");

            migrationBuilder.DropTable(
                name: "SatisIadeler");

            migrationBuilder.DeleteData(
                table: "GiderKategoriler",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "GiderKategoriler",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DropColumn(
                name: "IptalEdildi",
                table: "Satislar");

            migrationBuilder.DropColumn(
                name: "IptalNedeni",
                table: "Satislar");

            migrationBuilder.DropColumn(
                name: "IptalTarihi",
                table: "Satislar");

            migrationBuilder.DropColumn(
                name: "KismiIade",
                table: "Satislar");
        }
    }
}
