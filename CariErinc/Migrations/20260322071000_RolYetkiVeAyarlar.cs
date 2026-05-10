using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CariErinc.Migrations
{
    /// <inheritdoc />
    public partial class RolYetkiVeAyarlar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AktifMi",
                table: "Kullanicilar",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "IsletmeAyarlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Anahtar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Deger = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsletmeAyarlar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roller",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ad = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Aciklama = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsAdmin = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roller", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KullaniciRoller",
                columns: table => new
                {
                    KullaniciId = table.Column<int>(type: "integer", nullable: false),
                    RolId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KullaniciRoller", x => new { x.KullaniciId, x.RolId });
                    table.ForeignKey(
                        name: "FK_KullaniciRoller_Kullanicilar_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KullaniciRoller_Roller_RolId",
                        column: x => x.RolId,
                        principalTable: "Roller",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolYetkiler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RolId = table.Column<int>(type: "integer", nullable: false),
                    ControllerAdi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActionAdi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SidebarGrubu = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    SidebarGoruntuAdi = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SidebarSira = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolYetkiler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolYetkiler_Roller_RolId",
                        column: x => x.RolId,
                        principalTable: "Roller",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "IsletmeAyarlar",
                columns: new[] { "Id", "Anahtar", "Deger" },
                values: new object[,]
                {
                    { 1, "DukkanAdi", "Kırtasiye Dükkanı" },
                    { 2, "IsletmeTipi", "Kirtasiye" },
                    { 3, "Adres", "" },
                    { 4, "Telefon", "" }
                });

            migrationBuilder.InsertData(
                table: "Roller",
                columns: new[] { "Id", "Aciklama", "Ad", "IsAdmin" },
                values: new object[] { 1, "Tam yetkili yönetici", "Admin", true });

            migrationBuilder.CreateIndex(
                name: "IX_IsletmeAyarlar_Anahtar",
                table: "IsletmeAyarlar",
                column: "Anahtar",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KullaniciRoller_RolId",
                table: "KullaniciRoller",
                column: "RolId");

            migrationBuilder.CreateIndex(
                name: "IX_RolYetkiler_RolId",
                table: "RolYetkiler",
                column: "RolId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IsletmeAyarlar");

            migrationBuilder.DropTable(
                name: "KullaniciRoller");

            migrationBuilder.DropTable(
                name: "RolYetkiler");

            migrationBuilder.DropTable(
                name: "Roller");

            migrationBuilder.DropColumn(
                name: "AktifMi",
                table: "Kullanicilar");
        }
    }
}
