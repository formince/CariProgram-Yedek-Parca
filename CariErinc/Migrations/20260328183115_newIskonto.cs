using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CariErinc.Migrations
{
    /// <inheritdoc />
    public partial class newIskonto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Iskonto1",
                table: "AlisDetaylari",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Iskonto2",
                table: "AlisDetaylari",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.InsertData(
                table: "IsletmeAyarlar",
                columns: new[] { "Id", "Anahtar", "Deger" },
                values: new object[] { 5, "VarsayilanKdv", "20" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "IsletmeAyarlar",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DropColumn(
                name: "Iskonto1",
                table: "AlisDetaylari");

            migrationBuilder.DropColumn(
                name: "Iskonto2",
                table: "AlisDetaylari");
        }
    }
}
