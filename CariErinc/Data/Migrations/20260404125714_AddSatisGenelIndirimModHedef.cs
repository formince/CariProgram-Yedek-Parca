using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CariErinc.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSatisGenelIndirimModHedef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GenelIndirimHedefToplam",
                table: "Satislar",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<byte>(
                name: "GenelIndirimHesapModu",
                table: "Satislar",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GenelIndirimHedefToplam",
                table: "Satislar");

            migrationBuilder.DropColumn(
                name: "GenelIndirimHesapModu",
                table: "Satislar");
        }
    }
}
