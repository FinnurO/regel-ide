using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class VedtaksformOgIkraftstatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // [Ny, issue #446 PR 1] IF NOT EXISTS av samme grunn som i NedlagteAktorerOgEndringsmarkor: migrasjonstestene (HistoriskSkjema) legger
            // kolonnene til på forhånd fordi de skriver rettskilder og noder med dagens modell mot et historisk skjema.
            migrationBuilder.Sql("ALTER TABLE rettskilder ADD COLUMN IF NOT EXISTS vedtaksform text; "
                + "ALTER TABLE rettskilder ADD COLUMN IF NOT EXISTS vedtaksform_grunnlag text; "
                + "ALTER TABLE rettskilder ADD COLUMN IF NOT EXISTS vedtaksform_kunnskapsstatus text; "
                + "ALTER TABLE rettskilder ADD COLUMN IF NOT EXISTS vedtaksform_regelversjon text; "
                + "ALTER TABLE rettskilder ADD COLUMN IF NOT EXISTS ikraft_status text; "
                + "ALTER TABLE rettskilder ADD COLUMN IF NOT EXISTS ikraft_status_grunnlag text; "
                + "ALTER TABLE rettskilde_noder ADD COLUMN IF NOT EXISTS endringsrolle text;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rettskilder_ikraft_status",
                table: "rettskilder",
                sql: "ikraft_status IS NULL OR ikraft_status IN ('ja', 'delvis', 'ikke_i_kraft', 'utlopt', 'ukjent')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rettskilder_vedtaksform",
                table: "rettskilder",
                sql: "vedtaksform IS NULL OR vedtaksform IN ('rent_endringsvedtak', 'rent_ikrafttredelsesvedtak', 'rent_opphevingsvedtak', 'blandet', 'selvstendig', 'ukjent')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rettskilde_noder_endringsrolle",
                table: "rettskilde_noder",
                sql: "endringsrolle IS NULL OR endringsrolle IN ('endringsinstruks', 'sitert_tekst', 'ikrafttredelse', 'oppheving')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rettskilder_ikraft_status",
                table: "rettskilder");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rettskilder_vedtaksform",
                table: "rettskilder");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rettskilde_noder_endringsrolle",
                table: "rettskilde_noder");

            migrationBuilder.DropColumn(
                name: "ikraft_status",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "ikraft_status_grunnlag",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "vedtaksform",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "vedtaksform_grunnlag",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "vedtaksform_kunnskapsstatus",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "vedtaksform_regelversjon",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "endringsrolle",
                table: "rettskilde_noder");
        }
    }
}
