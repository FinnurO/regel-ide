using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilBruker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "brukere",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    navn = table.Column<string>(type: "text", nullable: false),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rolle = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("brukere_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_brukere_virksomheter_virksomhet_id",
                        column: x => x.virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_brukere_virksomhet",
                table: "brukere",
                column: "virksomhet_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "brukere");
        }
    }
}
