using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilTverrVirksomhetForslag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "foreslatt_av_virksomhet_id",
                table: "proveniens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_proveniens_foreslatt_av_virksomhet_id",
                table: "proveniens",
                column: "foreslatt_av_virksomhet_id");

            migrationBuilder.AddForeignKey(
                name: "FK_proveniens_virksomheter_foreslatt_av_virksomhet_id",
                table: "proveniens",
                column: "foreslatt_av_virksomhet_id",
                principalTable: "virksomheter",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_proveniens_virksomheter_foreslatt_av_virksomhet_id",
                table: "proveniens");

            migrationBuilder.DropIndex(
                name: "IX_proveniens_foreslatt_av_virksomhet_id",
                table: "proveniens");

            migrationBuilder.DropColumn(
                name: "foreslatt_av_virksomhet_id",
                table: "proveniens");
        }
    }
}
