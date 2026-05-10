using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CariErinc.Migrations
{
    /// <inheritdoc />
    public partial class VadeliAlisEkle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ToplamBorc",
                table: "Tedarikciler",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "KalanBorc",
                table: "Alislar",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "OdemeTipi",
                table: "Alislar",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "OdenenTutar",
                table: "Alislar",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "OdenmeDurumu_Odendi",
                table: "Alislar",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "VadeTarihi",
                table: "Alislar",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AlisOdemeleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AlisId = table.Column<int>(type: "integer", nullable: false),
                    OdemeTutari = table.Column<decimal>(type: "numeric", nullable: false),
                    OdemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Aciklama = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlisOdemeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlisOdemeleri_Alislar_AlisId",
                        column: x => x.AlisId,
                        principalTable: "Alislar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlisOdemeleri_AlisId",
                table: "AlisOdemeleri",
                column: "AlisId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlisOdemeleri");

            migrationBuilder.DropColumn(
                name: "ToplamBorc",
                table: "Tedarikciler");

            migrationBuilder.DropColumn(
                name: "KalanBorc",
                table: "Alislar");

            migrationBuilder.DropColumn(
                name: "OdemeTipi",
                table: "Alislar");

            migrationBuilder.DropColumn(
                name: "OdenenTutar",
                table: "Alislar");

            migrationBuilder.DropColumn(
                name: "OdenmeDurumu_Odendi",
                table: "Alislar");

            migrationBuilder.DropColumn(
                name: "VadeTarihi",
                table: "Alislar");
        }
    }
}
