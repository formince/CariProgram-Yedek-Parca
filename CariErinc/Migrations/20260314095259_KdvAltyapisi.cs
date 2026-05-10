using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CariErinc.Migrations
{
    /// <inheritdoc />
    public partial class KdvAltyapisi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KdvOrani",
                table: "Urunler",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "KdvOrani",
                table: "SatisDetaylari",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "KdvTutari",
                table: "SatisDetaylari",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "KdvOrani",
                table: "AlisDetaylari",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "KdvTutari",
                table: "AlisDetaylari",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KdvOrani",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "KdvOrani",
                table: "SatisDetaylari");

            migrationBuilder.DropColumn(
                name: "KdvTutari",
                table: "SatisDetaylari");

            migrationBuilder.DropColumn(
                name: "KdvOrani",
                table: "AlisDetaylari");

            migrationBuilder.DropColumn(
                name: "KdvTutari",
                table: "AlisDetaylari");
        }
    }
}
