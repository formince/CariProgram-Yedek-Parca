using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CariErinc.Migrations
{
    /// <inheritdoc />
    public partial class IndirimEkle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GenelIndirimOrani",
                table: "Satislar",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GenelIndirimTutari",
                table: "Satislar",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IndirimSonrasiToplam",
                table: "Satislar",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IndirimOrani",
                table: "SatisDetaylari",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IndirimTutari",
                table: "SatisDetaylari",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetTutar",
                table: "SatisDetaylari",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GenelIndirimOrani",
                table: "Satislar");

            migrationBuilder.DropColumn(
                name: "GenelIndirimTutari",
                table: "Satislar");

            migrationBuilder.DropColumn(
                name: "IndirimSonrasiToplam",
                table: "Satislar");

            migrationBuilder.DropColumn(
                name: "IndirimOrani",
                table: "SatisDetaylari");

            migrationBuilder.DropColumn(
                name: "IndirimTutari",
                table: "SatisDetaylari");

            migrationBuilder.DropColumn(
                name: "NetTutar",
                table: "SatisDetaylari");
        }
    }
}
