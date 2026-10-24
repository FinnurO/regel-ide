using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class NedlagteAktorerOgEndringsmarkor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // [Ny, issue #395] IF NOT EXISTS av samme grunn som i Organiseringskilder: migrasjonstestene (HistoriskSkjema) legger kolonnene til
            // på forhånd fordi de skriver virksomheter og rettskilder med dagens modell mot et historisk skjema.
            migrationBuilder.Sql("ALTER TABLE virksomheter ADD COLUMN IF NOT EXISTS livsstatus text; "
                + "ALTER TABLE virksomheter ADD COLUMN IF NOT EXISTS gyldig_fra date; "
                + "ALTER TABLE virksomheter ADD COLUMN IF NOT EXISTS gyldig_til date; "
                + "ALTER TABLE rettskilder ADD COLUMN IF NOT EXISTS rent_endringsvedtak boolean; "
                + "ALTER TABLE rettskilder ADD COLUMN IF NOT EXISTS rent_endringsvedtak_grunnlag text;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_virksomheter_livsstatus",
                table: "virksomheter",
                sql: "livsstatus IS NULL OR livsstatus = 'nedlagt'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_virksomheter_livsstatus",
                table: "virksomheter");

            migrationBuilder.DropColumn(
                name: "gyldig_fra",
                table: "virksomheter");

            migrationBuilder.DropColumn(
                name: "gyldig_til",
                table: "virksomheter");

            migrationBuilder.DropColumn(
                name: "livsstatus",
                table: "virksomheter");

            migrationBuilder.DropColumn(
                name: "rent_endringsvedtak",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "rent_endringsvedtak_grunnlag",
                table: "rettskilder");
        }
    }
}
