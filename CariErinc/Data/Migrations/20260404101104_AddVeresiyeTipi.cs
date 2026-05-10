using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CariErinc.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVeresiyeTipi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Tip",
                table: "Veresiyeler",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tip",
                table: "Veresiyeler");
        }
    }
}
