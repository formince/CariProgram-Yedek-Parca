using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CariErinc.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVeresiyeOdemeAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KullaniciId",
                table: "VeresiyeOdemeler",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OdemeTipi",
                table: "VeresiyeOdemeler",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KullaniciId",
                table: "VeresiyeOdemeler");

            migrationBuilder.DropColumn(
                name: "OdemeTipi",
                table: "VeresiyeOdemeler");
        }
    }
}
