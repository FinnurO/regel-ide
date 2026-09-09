using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, nemnd/sekretariat-runden, 2026-09-09] REN DATAMIGRASJON — ingen skjemaendring.
    ///
    /// <para>
    /// <see cref="VirksomhetKandidatTjeneste.GodkjennAsync"/> satte taggens
    /// <see cref="TekstTaggEntitet.VirksomhetId"/> til det TAGGEDE organet
    /// (<c>kandidat.VirksomhetId</c>) i stedet for til virksomheten som godkjente. Feltet er
    /// eierskap/tenancy — «virksomhetens eget arbeidsprodukt», se feltets egen kommentar — og
    /// <c>GET /api/rettskilder/{id}/tagger</c> viser bare innlogget virksomhets egne tagger. Følgen
    /// var at hver godkjente kandidat lagde en tagg eid av organet den handlet OM, altså en tagg
    /// ingen kunne se: organene i katalogen har ingen brukere. Koden er rettet; denne migrasjonen
    /// flytter eierskapet på radene som alt er opprettet.
    /// </para>
    ///
    /// <para>
    /// <b>Hvem er riktig eier for en eksisterende rad?</b> Den som godkjente. Det er ikke lagret som
    /// en id, bare som navn i <c>opprettet_av</c> — så raden kobles til <c>brukere.navn</c>, og KUN
    /// når navnet peker entydig på én virksomhet. Er navnet tvetydig eller ukjent, står raden urørt
    /// framfor å bli gitt en gjettet eier (samme «ingen gjettet fallback» som ellers i kodebasen).
    /// </para>
    ///
    /// <para>
    /// <b>Avgrensning:</b> kun rader der eieren ER det taggede organet — taggens <c>virksomhet_id</c>
    /// lik <c>virksomhet_referanse_id</c> på navneformen taggen peker på. Det er nøyaktig signaturen
    /// til feilen. En virksomhet som med rette har tagget SEG SELV ser lik ut, men flyttes da til
    /// brukerens egen virksomhet, altså seg selv — en no-op.
    /// </para>
    ///
    /// <para>
    /// <b>Kollisjoner:</b> <c>tekst_tagger_unik_tagg</c> dekker (virksomhet, rettskilde, node,
    /// offsets, kind, ref_id). Finnes taggen ALT for den riktige eieren — typisk fordi noen hadde
    /// tagget samme sted manuelt før sveipet ble godkjent — kan raden ikke flyttes. Da slettes den
    /// feileide raden i stedet: den er en eksakt dublett av en rad som allerede finnes med riktig
    /// eier, og et eierskap ingen kan se er ikke verdt å bevare. Sletting skjer KUN når en slik
    /// identisk rad er bekreftet å finnes.
    /// </para>
    ///
    /// <para>
    /// <c>Down</c> er bevisst tom: etter <c>Up</c> er de flyttede radene ikke skillbare fra tagger som
    /// hadde riktig eier hele tiden, så en reversering ville måttet gjette hvilke som skulle tilbake.
    /// </para>
    /// </summary>
    public partial class EierskiftePaTaggerFraGodkjentKandidat : Migration
    {
        /// <summary>
        /// Navn → entydig virksomhet. Brukes av begge setningene under. «b."Id"» med anførselstegn
        /// fordi bare de eksplisitt mappede kolonnene er snake_case; primærnøkkelen heter fortsatt
        /// "Id" i basen.
        /// </summary>
        private const string FeileideTagger = """
            WITH entydig_bruker AS (
                SELECT navn, (array_agg(DISTINCT virksomhet_id))[1] AS virksomhet_id
                FROM brukere
                GROUP BY navn
                HAVING COUNT(DISTINCT virksomhet_id) = 1
            )
            SELECT t."Id" AS tagg_id, eb.virksomhet_id AS riktig_eier
            FROM tekst_tagger AS t
            JOIN begreper AS b ON b."Id" = t.ref_id AND b.begrepskategori = 'virksomhet'
            JOIN entydig_bruker AS eb ON eb.navn = t.opprettet_av
            WHERE t.virksomhet_id = b.virksomhet_referanse_id
            """;

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Slett de feileide radene som IKKE kan flyttes fordi en identisk rad alt finnes for
            //    riktig eier.
            migrationBuilder.Sql($"""
                DELETE FROM tekst_tagger AS d
                USING ({FeileideTagger}) AS f
                WHERE d."Id" = f.tagg_id
                  AND EXISTS (
                      SELECT 1 FROM tekst_tagger AS x
                      WHERE x.virksomhet_id = f.riktig_eier
                        AND x.rettskilde_id = d.rettskilde_id
                        AND x.node_eid = d.node_eid
                        AND x.start_offset = d.start_offset
                        AND x.end_offset = d.end_offset
                        AND x.kind = d.kind
                        AND x.ref_id IS NOT DISTINCT FROM d.ref_id
                  );
                """);

            // 2) Flytt resten.
            migrationBuilder.Sql($"""
                UPDATE tekst_tagger AS t
                SET virksomhet_id = f.riktig_eier
                FROM ({FeileideTagger}) AS f
                WHERE t."Id" = f.tagg_id;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Bevisst tom — se klassekommentaren.
        }
    }
}
