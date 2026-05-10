using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CariErinc.Migrations
{
    /// <inheritdoc />
    public partial class AlisMaliyetiEkle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AlisFiyati",
                table: "Urunler",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "SonAlisTarihi",
                table: "Urunler",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AlisBirimFiyati",
                table: "SatisDetaylari",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlisFiyati",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "SonAlisTarihi",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "AlisBirimFiyati",
                table: "SatisDetaylari");
        }
    }
}
