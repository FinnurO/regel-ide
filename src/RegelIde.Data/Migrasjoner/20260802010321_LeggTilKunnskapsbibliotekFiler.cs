using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilKunnskapsbibliotekFiler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "kunnskapsbibliotek_filer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    filnavn = table.Column<string>(type: "text", nullable: false),
                    filtype = table.Column<string>(type: "text", nullable: false),
                    innhold = table.Column<byte[]>(type: "bytea", nullable: false),
                    utvunnet_tekst = table.Column<string>(type: "text", nullable: false),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("kunnskapsbibliotek_filer_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kunnskapsbibliotek_filer_virksomheter_virksomhet_id",
                        column: x => x.virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_kunnskapsbibliotek_filer_virksomhet",
                table: "kunnskapsbibliotek_filer",
                column: "virksomhet_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kunnskapsbibliotek_filer");
        }
    }
}
