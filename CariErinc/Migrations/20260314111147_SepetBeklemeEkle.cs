using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CariErinc.Migrations
{
    /// <inheritdoc />
    public partial class SepetBeklemeEkle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Durum",
                table: "Satislar",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Durum",
                table: "Satislar");
        }
    }
}
