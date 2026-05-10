using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CariErinc.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCariSafeTransition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CariId",
                table: "Veresiyeler",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CariId",
                table: "Urunler",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CariId",
                table: "Tedarikciler",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CariId",
                table: "Satislar",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CariId",
                table: "Musteriler",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CariId",
                table: "Alislar",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CariBaglantilar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MusteriId = table.Column<int>(type: "integer", nullable: false),
                    TedarikciId = table.Column<int>(type: "integer", nullable: false),
                    OlusturulmaTarihi = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CariBaglantilar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CariBaglantilar_Musteriler_MusteriId",
                        column: x => x.MusteriId,
                        principalTable: "Musteriler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CariBaglantilar_Tedarikciler_TedarikciId",
                        column: x => x.TedarikciId,
                        principalTable: "Tedarikciler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cariler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ad = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    YetkiliKisi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Telefon = table.Column<string>(type: "text", nullable: true),
                    Adres = table.Column<string>(type: "text", nullable: true),
                    Rol = table.Column<int>(type: "integer", nullable: false),
                    AktifMi = table.Column<bool>(type: "boolean", nullable: false),
                    OlusturulmaTarihi = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cariler", x => x.Id);
                });

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    rec RECORD;
                    yeniCariId INTEGER;
                BEGIN
                    FOR rec IN SELECT "Id", "Ad", "Soyad", "Telefon", "Adres", "OlusturulmaTarihi" FROM "Musteriler"
                    LOOP
                        INSERT INTO "Cariler" ("Ad", "YetkiliKisi", "Telefon", "Adres", "Rol", "AktifMi", "OlusturulmaTarihi")
                        VALUES (TRIM(COALESCE(rec."Ad", '') || CASE WHEN COALESCE(rec."Soyad", '') = '' THEN '' ELSE ' ' || rec."Soyad" END),
                                NULL,
                                rec."Telefon",
                                rec."Adres",
                                1,
                                TRUE,
                                COALESCE(rec."OlusturulmaTarihi", NOW()))
                        RETURNING "Id" INTO yeniCariId;

                        UPDATE "Musteriler" SET "CariId" = yeniCariId WHERE "Id" = rec."Id";
                    END LOOP;
                END
                $$;
                """);

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    rec RECORD;
                    yeniCariId INTEGER;
                BEGIN
                    FOR rec IN SELECT "Id", "Ad", "YetkiliKisi", "Telefon", "Adres", "OlusturulmaTarihi" FROM "Tedarikciler"
                    LOOP
                        INSERT INTO "Cariler" ("Ad", "YetkiliKisi", "Telefon", "Adres", "Rol", "AktifMi", "OlusturulmaTarihi")
                        VALUES (rec."Ad",
                                rec."YetkiliKisi",
                                rec."Telefon",
                                rec."Adres",
                                2,
                                TRUE,
                                COALESCE(rec."OlusturulmaTarihi", NOW()))
                        RETURNING "Id" INTO yeniCariId;

                        UPDATE "Tedarikciler" SET "CariId" = yeniCariId WHERE "Id" = rec."Id";
                    END LOOP;
                END
                $$;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Satislar" s
                SET "CariId" = m."CariId"
                FROM "Musteriler" m
                WHERE s."MusteriId" = m."Id"
                  AND s."CariId" IS NULL;

                UPDATE "Veresiyeler" v
                SET "CariId" = m."CariId"
                FROM "Musteriler" m
                WHERE v."MusteriId" = m."Id"
                  AND v."CariId" IS NULL;

                UPDATE "Alislar" a
                SET "CariId" = t."CariId"
                FROM "Tedarikciler" t
                WHERE a."TedarikciId" = t."Id"
                  AND a."CariId" IS NULL;

                UPDATE "Urunler" u
                SET "CariId" = t."CariId"
                FROM "Tedarikciler" t
                WHERE u."TedarikciId" = t."Id"
                  AND u."CariId" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Veresiyeler_CariId",
                table: "Veresiyeler",
                column: "CariId");

            migrationBuilder.CreateIndex(
                name: "IX_Urunler_CariId",
                table: "Urunler",
                column: "CariId");

            migrationBuilder.CreateIndex(
                name: "IX_Tedarikciler_CariId",
                table: "Tedarikciler",
                column: "CariId");

            migrationBuilder.CreateIndex(
                name: "IX_Satislar_CariId",
                table: "Satislar",
                column: "CariId");

            migrationBuilder.CreateIndex(
                name: "IX_Musteriler_CariId",
                table: "Musteriler",
                column: "CariId");

            migrationBuilder.CreateIndex(
                name: "IX_Alislar_CariId",
                table: "Alislar",
                column: "CariId");

            migrationBuilder.CreateIndex(
                name: "IX_CariBaglantilar_MusteriId",
                table: "CariBaglantilar",
                column: "MusteriId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CariBaglantilar_TedarikciId",
                table: "CariBaglantilar",
                column: "TedarikciId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Alislar_Cariler_CariId",
                table: "Alislar",
                column: "CariId",
                principalTable: "Cariler",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Musteriler_Cariler_CariId",
                table: "Musteriler",
                column: "CariId",
                principalTable: "Cariler",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Satislar_Cariler_CariId",
                table: "Satislar",
                column: "CariId",
                principalTable: "Cariler",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tedarikciler_Cariler_CariId",
                table: "Tedarikciler",
                column: "CariId",
                principalTable: "Cariler",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Urunler_Cariler_CariId",
                table: "Urunler",
                column: "CariId",
                principalTable: "Cariler",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Veresiyeler_Cariler_CariId",
                table: "Veresiyeler",
                column: "CariId",
                principalTable: "Cariler",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alislar_Cariler_CariId",
                table: "Alislar");

            migrationBuilder.DropForeignKey(
                name: "FK_Musteriler_Cariler_CariId",
                table: "Musteriler");

            migrationBuilder.DropForeignKey(
                name: "FK_Satislar_Cariler_CariId",
                table: "Satislar");

            migrationBuilder.DropForeignKey(
                name: "FK_Tedarikciler_Cariler_CariId",
                table: "Tedarikciler");

            migrationBuilder.DropForeignKey(
                name: "FK_Urunler_Cariler_CariId",
                table: "Urunler");

            migrationBuilder.DropForeignKey(
                name: "FK_Veresiyeler_Cariler_CariId",
                table: "Veresiyeler");

            migrationBuilder.DropTable(
                name: "CariBaglantilar");

            migrationBuilder.DropTable(
                name: "Cariler");

            migrationBuilder.DropIndex(
                name: "IX_Veresiyeler_CariId",
                table: "Veresiyeler");

            migrationBuilder.DropIndex(
                name: "IX_Urunler_CariId",
                table: "Urunler");

            migrationBuilder.DropIndex(
                name: "IX_Tedarikciler_CariId",
                table: "Tedarikciler");

            migrationBuilder.DropIndex(
                name: "IX_Satislar_CariId",
                table: "Satislar");

            migrationBuilder.DropIndex(
                name: "IX_Musteriler_CariId",
                table: "Musteriler");

            migrationBuilder.DropIndex(
                name: "IX_Alislar_CariId",
                table: "Alislar");

            migrationBuilder.DropColumn(
                name: "CariId",
                table: "Veresiyeler");

            migrationBuilder.DropColumn(
                name: "CariId",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "CariId",
                table: "Tedarikciler");

            migrationBuilder.DropColumn(
                name: "CariId",
                table: "Satislar");

            migrationBuilder.DropColumn(
                name: "CariId",
                table: "Musteriler");

            migrationBuilder.DropColumn(
                name: "CariId",
                table: "Alislar");
        }
    }
}
