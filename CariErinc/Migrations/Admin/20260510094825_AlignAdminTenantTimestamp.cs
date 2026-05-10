using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CariErinc.Migrations.Admin
{
    /// <inheritdoc />
    public partial class AlignAdminTenantTimestamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "OlusturulmaTarihi",
                table: "TenantKayitlar",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "OlusturulmaTarihi",
                table: "TenantKayitlar",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");
        }
    }
}
