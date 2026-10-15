using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class AvvistSomStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "avvisning_begrunnelse",
                table: "strukturkanter",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_avvisning_begrunnelse",
                table: "strukturkanter",
                sql: "avvisning_begrunnelse IS NULL OR status = 'avvist'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_avvist_hvem",
                table: "strukturkanter",
                sql: "status <> 'avvist' OR (sist_endret_av IS NOT NULL AND sist_endret_tidspunkt IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_avvisning_begrunnelse",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_avvist_hvem",
                table: "strukturkanter");

            migrationBuilder.DropColumn(
                name: "avvisning_begrunnelse",
                table: "strukturkanter");
        }
    }
}
