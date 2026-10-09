using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, issue #353 «pliktrelasjoner mellom parter (kategori P) og nodetypen ordning», Johanns godkjenning 2026-10-08] Ny
    /// kategori P (plikt overfor motpart) i strukturkanter og typekonfigurasjonen, ny P-egenskap <c>modalitet</c> (skal/kan/bør,
    /// NULL = ikke angitt), P uten til-node (betalingsmottakeren er null når teksten ikke sier det) og P innad i en klasse/rolle
    /// i selvkant-CHECK-en; ny aktørtype <c>ordning</c> og <c>ordningstype</c> (trygdeordning/fond/tilskuddsordning) på
    /// virksomheter. Datadelen i <see cref="PliktMigrering"/>: de åtte nye typene (P ×6, R forvaltes_av, G tilhorer) og flytting
    /// av eventuelle R samarbeider_med/bistar-kanter til P. Teller før/etter og avbryter ved avvik; <c>Down</c> snur nøyaktig.
    /// Kjørt etter #352 (ingen stablede migrasjoner, sakens AC1).
    /// </summary>
    public partial class PliktOgOrdning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_virksomheter_aktortype",
                table: "virksomheter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_ikke_selv",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_kategori",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_til_pakrevd",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_relasjonstype_konfigurasjon_kategori",
                table: "relasjonstype_konfigurasjon");

            migrationBuilder.AddColumn<string>(
                name: "ordningstype",
                table: "virksomheter",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modalitet",
                table: "strukturkanter",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_virksomheter_aktortype",
                table: "virksomheter",
                sql: "aktortype IS NULL OR aktortype IN ('rettssubjekt', 'organ', 'organisatorisk_enhet', 'ordning')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_virksomheter_ordningstype",
                table: "virksomheter",
                sql: "ordningstype IS NULL OR (aktortype = 'ordning' AND ordningstype IN ('trygdeordning', 'fond', 'tilskuddsordning'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_ikke_selv",
                table: "strukturkanter",
                sql: "(kategori = 'K' AND typekode = 'normgivning') OR (kategori = 'P' AND fra_begrep_id IS NOT NULL) OR ((fra_virksomhet_id IS NULL OR til_virksomhet_id IS NULL OR fra_virksomhet_id <> til_virksomhet_id) AND (fra_begrep_id IS NULL OR til_begrep_id IS NULL OR fra_begrep_id <> til_begrep_id))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_kategori",
                table: "strukturkanter",
                sql: "kategori IN ('R', 'K', 'P', 'M', 'O', 'A', 'G', 'I', 'T')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_modalitet",
                table: "strukturkanter",
                sql: "modalitet IS NULL OR (kategori = 'P' AND modalitet IN ('skal', 'kan', 'bor'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_til_pakrevd",
                table: "strukturkanter",
                sql: "kategori IN ('K', 'P', 'T') OR til_virksomhet_id IS NOT NULL OR til_begrep_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_relasjonstype_konfigurasjon_kategori",
                table: "relasjonstype_konfigurasjon",
                sql: "kategori IN ('R', 'K', 'P', 'M', 'O', 'A', 'G', 'I', 'T')");

            // Datadelen etter de nye CHECK-ene: P-radene i konfigurasjonen og P-kantene krever at 'P' er lov.
            migrationBuilder.Sql(PliktMigrering.UpSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_virksomheter_aktortype",
                table: "virksomheter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_virksomheter_ordningstype",
                table: "virksomheter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_ikke_selv",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_kategori",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_modalitet",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_til_pakrevd",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_relasjonstype_konfigurasjon_kategori",
                table: "relasjonstype_konfigurasjon");

            // Datadelen FØR kolonnene fjernes og de gamle CHECK-ene legges inn igjen — Down leser proveniensradene, avbryter hvis
            // det finnes opplysninger som ikke kan uttrykkes før #353, og fjerner P-typene.
            migrationBuilder.Sql(PliktMigrering.DownSql);

            migrationBuilder.DropColumn(
                name: "ordningstype",
                table: "virksomheter");

            migrationBuilder.DropColumn(
                name: "modalitet",
                table: "strukturkanter");

            migrationBuilder.AddCheckConstraint(
                name: "ck_virksomheter_aktortype",
                table: "virksomheter",
                sql: "aktortype IS NULL OR aktortype IN ('rettssubjekt', 'organ', 'organisatorisk_enhet')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_ikke_selv",
                table: "strukturkanter",
                sql: "(kategori = 'K' AND typekode = 'normgivning') OR ((fra_virksomhet_id IS NULL OR til_virksomhet_id IS NULL OR fra_virksomhet_id <> til_virksomhet_id) AND (fra_begrep_id IS NULL OR til_begrep_id IS NULL OR fra_begrep_id <> til_begrep_id))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_kategori",
                table: "strukturkanter",
                sql: "kategori IN ('R', 'K', 'M', 'O', 'A', 'G', 'I', 'T')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_til_pakrevd",
                table: "strukturkanter",
                sql: "kategori IN ('K', 'T') OR til_virksomhet_id IS NOT NULL OR til_begrep_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_relasjonstype_konfigurasjon_kategori",
                table: "relasjonstype_konfigurasjon",
                sql: "kategori IN ('R', 'K', 'M', 'O', 'A', 'G', 'I', 'T')");
        }
    }
}
