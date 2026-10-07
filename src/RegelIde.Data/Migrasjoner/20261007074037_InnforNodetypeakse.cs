using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, issue #310 «Strukturmodell 5: nodetype-akse», 2026-10-07] Skjema + data i ÉN migrasjon:
    /// <list type="bullet">
    /// <item><c>virksomheter.aktortype</c> (nullbar, CHECK) og automatisk utfylling KUN der det er entydig
    /// (<see cref="NodetypeReklassifisering.AktortypeSql"/>).</item>
    /// <item><c>begrepskategori</c> får <c>klasse</c>/<c>rolle</c>/<c>omrade</c>/<c>organ</c>;
    /// <c>administrativ_inndeling</c> slås inn i <c>omrade</c>; <c>gruppe</c> BLIR STÅENDE i CHECK-en
    /// (andre miljøer kan ha rader som ikke er på lista). De partielle unike indeksene (inkl. #298s
    /// faste/nasjonale) omdøpes til «nodebegrep» og dekker alle kategoriene med gruppefunksjon.</item>
    /// <item>De 13 gruppebegrepene reklassifiseres etter Johanns godkjente liste
    /// (<see cref="NodetypeReklassifisering.Sql"/> — se den klassen for identifisering, sammenslåing og
    /// «stortinget»-regelen).</item>
    /// </list>
    /// <para>
    /// <b>Rekkefølgen er bærende</b> og er derfor håndredigert etter <c>dotnet ef migrations add</c>:
    /// gamle CHECK-er og indekser droppes FØRST, så kjøres data-SQL-en (som skriver verdier den gamle
    /// CHECK-en ikke tillater, og flytter rader mellom indeks-filtrene), og FØRST DERETTER legges de nye
    /// skrankene på — slik at en eventuell dublett i et annet miljø feiler høylytt her i stedet for å bli
    /// stille akseptert.
    /// </para>
    /// <para>
    /// <b><c>Down</c> er bevisst upresis:</b> sammenslåinger reverseres ikke (de arkiverte radene blir
    /// stående arkivert), og alle typede kategorier settes tilbake til <c>gruppe</c> — typen kan ikke
    /// gjenskapes uten den kunnskapen <c>Up</c> la til. Det er nok til at skjemaet blir gyldig igjen.
    /// </para>
    /// </summary>
    public partial class InnforNodetypeakse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_navnekandidater_kategori",
                table: "navnekandidater");

            migrationBuilder.DropIndex(
                name: "ux_begreper_administrativ_inndeling_term_lovkilde",
                table: "begreper");

            migrationBuilder.DropIndex(
                name: "ux_begreper_gruppebegrep_fast_term",
                table: "begreper");

            migrationBuilder.DropIndex(
                name: "ux_begreper_gruppebegrep_term_lovkilde",
                table: "begreper");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_begrepskategori",
                table: "begreper");

            migrationBuilder.AddColumn<string>(
                name: "aktortype",
                table: "virksomheter",
                type: "text",
                nullable: true);

            // Data — se klassekommentaren for hvorfor dette står MELLOM drop og add.
            migrationBuilder.Sql(NodetypeReklassifisering.Sql);
            migrationBuilder.Sql(NodetypeReklassifisering.AktortypeSql);

            migrationBuilder.AddCheckConstraint(
                name: "ck_virksomheter_aktortype",
                table: "virksomheter",
                sql: "aktortype IS NULL OR aktortype IN ('rettssubjekt', 'organ', 'organisatorisk_enhet')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_navnekandidater_kategori",
                table: "navnekandidater",
                sql: "kategori IN ('virksomhet', 'gruppe', 'klasse', 'rolle', 'omrade')");

            migrationBuilder.CreateIndex(
                name: "ux_begreper_nodebegrep_fast_term",
                table: "begreper",
                column: "term",
                unique: true,
                filter: "begrepskategori IN ('gruppe', 'klasse', 'rolle', 'omrade', 'organ') AND entitetsstatus = 'gjeldende' AND lovkilde_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_begreper_nodebegrep_term_lovkilde",
                table: "begreper",
                columns: new[] { "term", "lovkilde_id" },
                unique: true,
                filter: "begrepskategori IN ('gruppe', 'klasse', 'rolle', 'omrade', 'organ') AND entitetsstatus = 'gjeldende'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_begrepskategori",
                table: "begreper",
                sql: "begrepskategori IS NULL OR begrepskategori IN ('virksomhet', 'gruppe', 'klasse', 'rolle', 'omrade', 'organ')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_virksomheter_aktortype",
                table: "virksomheter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_navnekandidater_kategori",
                table: "navnekandidater");

            migrationBuilder.DropIndex(
                name: "ux_begreper_nodebegrep_fast_term",
                table: "begreper");

            migrationBuilder.DropIndex(
                name: "ux_begreper_nodebegrep_term_lovkilde",
                table: "begreper");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_begrepskategori",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "aktortype",
                table: "virksomheter");

            // Typen kan ikke gjenskapes — se klassekommentaren.
            migrationBuilder.Sql("""
                UPDATE begreper SET begrepskategori = 'gruppe'
                WHERE begrepskategori IN ('klasse', 'rolle', 'omrade', 'organ');
                UPDATE navnekandidater SET kategori = 'gruppe'
                WHERE kategori IN ('klasse', 'rolle', 'omrade');
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_navnekandidater_kategori",
                table: "navnekandidater",
                sql: "kategori IN ('virksomhet', 'gruppe', 'administrativ_inndeling')");

            migrationBuilder.CreateIndex(
                name: "ux_begreper_administrativ_inndeling_term_lovkilde",
                table: "begreper",
                columns: new[] { "term", "lovkilde_id" },
                unique: true,
                filter: "begrepskategori = 'administrativ_inndeling' AND entitetsstatus = 'gjeldende'");

            migrationBuilder.CreateIndex(
                name: "ux_begreper_gruppebegrep_fast_term",
                table: "begreper",
                column: "term",
                unique: true,
                filter: "begrepskategori = 'gruppe' AND entitetsstatus = 'gjeldende' AND lovkilde_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_begreper_gruppebegrep_term_lovkilde",
                table: "begreper",
                columns: new[] { "term", "lovkilde_id" },
                unique: true,
                filter: "begrepskategori = 'gruppe' AND entitetsstatus = 'gjeldende'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_begrepskategori",
                table: "begreper",
                sql: "begrepskategori IS NULL OR begrepskategori IN ('virksomhet', 'gruppe', 'administrativ_inndeling')");
        }
    }
}
