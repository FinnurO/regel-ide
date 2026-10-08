using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, issue #312 «Strukturmodell 7: områderegister», 2026-10-08] Områdetype og -kode på begreper, ny identitet for
    /// registrerte områder (type + kode blant gjeldende rader), unntak for dem fra term-indeksene (Herøy og Våler finnes
    /// to ganger, Oslo er fylke og kommune), og kildetypen 'register' for strukturkanter (Strukturkanter.Register).
    /// Ingen data flyttes: registeret fylles av OmraderegisterSeed ved oppstart (gated, CLAUDE.md §4).
    /// </summary>
    public partial class InnforOmraderegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_kilde_type",
                table: "strukturkanter");

            migrationBuilder.DropIndex(
                name: "ux_begreper_nodebegrep_fast_term",
                table: "begreper");

            migrationBuilder.DropIndex(
                name: "ux_begreper_nodebegrep_term_lovkilde",
                table: "begreper");

            migrationBuilder.AddColumn<string>(
                name: "omradekode",
                table: "begreper",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "omradetype",
                table: "begreper",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_kilde_type",
                table: "strukturkanter",
                sql: "kilde_utenfor_korpus_type IS NULL OR kilde_utenfor_korpus_type IN ('kgl_res', 'instruks', 'tildelingsbrev', 'vedtekter', 'styrevedtak', 'forarbeider', 'nettside_annet', 'register')");

            migrationBuilder.CreateIndex(
                name: "ux_begreper_nodebegrep_fast_term",
                table: "begreper",
                column: "term",
                unique: true,
                filter: "begrepskategori IN ('gruppe', 'klasse', 'rolle', 'omrade') AND entitetsstatus = 'gjeldende' AND lovkilde_id IS NULL AND omradetype IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_begreper_nodebegrep_term_lovkilde",
                table: "begreper",
                columns: new[] { "term", "lovkilde_id" },
                unique: true,
                filter: "begrepskategori IN ('gruppe', 'klasse', 'rolle', 'omrade') AND entitetsstatus = 'gjeldende' AND omradetype IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_begreper_omrade_type_kode",
                table: "begreper",
                columns: new[] { "omradetype", "omradekode" },
                unique: true,
                filter: "begrepskategori = 'omrade' AND entitetsstatus = 'gjeldende' AND gyldig_til IS NULL AND omradekode IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_begreper_omrade_type_term_ukodet",
                table: "begreper",
                columns: new[] { "omradetype", "term" },
                unique: true,
                filter: "begrepskategori = 'omrade' AND entitetsstatus = 'gjeldende' AND gyldig_til IS NULL AND omradetype IS NOT NULL AND omradekode IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_omradekode",
                table: "begreper",
                sql: "omradekode IS NULL OR omradetype IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_omradetype",
                table: "begreper",
                sql: "omradetype IS NULL OR (begrepskategori = 'omrade' AND omradetype IN ('fylke', 'kommune', 'tettsted', 'lagsogn', 'lagdomme', 'helseregion', 'annet'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Registeret må ut FØR term-indeksene gjenopprettes (to «Herøy» ville veltet den unike indeksen) og før
            // kildetypen 'register' fjernes fra CHECK-en. Domstolvirksomhetene seeden opprettet blir stående — de er
            // ordinære Brreg-virksomheter og bryter ingen constraint.
            migrationBuilder.Sql("""
                DELETE FROM strukturkanter
                 WHERE kilde_utenfor_korpus_type = 'register'
                    OR fra_begrep_id IN (SELECT "Id" FROM begreper WHERE omradetype IS NOT NULL)
                    OR til_begrep_id IN (SELECT "Id" FROM begreper WHERE omradetype IS NOT NULL);
                DELETE FROM begreper WHERE omradetype IS NOT NULL;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_kilde_type",
                table: "strukturkanter");

            migrationBuilder.DropIndex(
                name: "ux_begreper_nodebegrep_fast_term",
                table: "begreper");

            migrationBuilder.DropIndex(
                name: "ux_begreper_nodebegrep_term_lovkilde",
                table: "begreper");

            migrationBuilder.DropIndex(
                name: "ux_begreper_omrade_type_kode",
                table: "begreper");

            migrationBuilder.DropIndex(
                name: "ux_begreper_omrade_type_term_ukodet",
                table: "begreper");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_omradekode",
                table: "begreper");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_omradetype",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "omradekode",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "omradetype",
                table: "begreper");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_kilde_type",
                table: "strukturkanter",
                sql: "kilde_utenfor_korpus_type IS NULL OR kilde_utenfor_korpus_type IN ('kgl_res', 'instruks', 'tildelingsbrev', 'vedtekter', 'styrevedtak', 'forarbeider', 'nettside_annet')");

            migrationBuilder.CreateIndex(
                name: "ux_begreper_nodebegrep_fast_term",
                table: "begreper",
                column: "term",
                unique: true,
                filter: "begrepskategori IN ('gruppe', 'klasse', 'rolle', 'omrade') AND entitetsstatus = 'gjeldende' AND lovkilde_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_begreper_nodebegrep_term_lovkilde",
                table: "begreper",
                columns: new[] { "term", "lovkilde_id" },
                unique: true,
                filter: "begrepskategori IN ('gruppe', 'klasse', 'rolle', 'omrade') AND entitetsstatus = 'gjeldende'");
        }
    }
}
