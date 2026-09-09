using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, nemnd/sekretariat-runden, 2026-09-09] REN DATAMIGRASJON — ingen skjemaendring.
    ///
    /// <para>
    /// Godkjenning av en virksomhetskandidat opprettet taggen med <c>kind = 'begrep'</c>, mens
    /// navnekandidat-veiviseren opprettet den med <c>kind = 'virksomhet'</c>. Samme påstand om samme
    /// organ havnet altså i to ULIKE lag i tagg-visningen, avhengig av hvilken oppdagelsesvei den kom
    /// fra — og i samme paragraf kunne halvparten av organnavnene ligge i «Begrep»-laget og
    /// halvparten i «Virksomhet»-laget. Johann 2026-09-09: «her flyter det litt sammen».
    /// <see cref="VirksomhetKandidatTjeneste.GodkjennAsync"/> er rettet; denne migrasjonen flytter de
    /// radene som alt er opprettet.
    /// </para>
    ///
    /// <para>
    /// Avgrensningen er nøyaktig «taggen peker på en NAVNEFORM»: <c>ref_id</c> må finnes i
    /// <c>begreper</c> med <c>begrepskategori = 'virksomhet'</c>. Gruppebegrep-tagger (f.eks.
    /// «kommunen» → gruppebegrep i kommuneloven) er ekte begrep-tagger og røres IKKE — de skal
    /// fortsatt ligge i «Begrep»-laget. Tagger uten <c>ref_id</c> (ukoblet, se
    /// <c>TekstTaggEntitet.RefId</c>) røres heller ikke: uten refererende rad kan vi ikke vite hva de
    /// betegner, og en gjettet omklassifisering er verre enn å la dem stå.
    /// </para>
    ///
    /// <para>
    /// <c>Down</c> kan ikke være presis: etter <c>Up</c> er de omklassifiserte radene ikke lenger
    /// skillbare fra tagger som ble opprettet i «Virksomhet»-laget fra starten (navneform-veiviseren
    /// sine). En reversering ville derfor også flyttet DEM til «Begrep», altså innført nøyaktig den
    /// feilen migrasjonen retter. Down er derfor bevisst en no-op — samme holdning som «ingen gjettet
    /// fallback» ellers i kodebasen: heller ingen handling enn en gal en.
    /// </para>
    /// </summary>
    public partial class OmklassifiserNavneformTaggerTilVirksomhetslaget : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Merk «b."Id"» med anførselstegn: bare de EKSPLISITT mappede kolonnene i
            // RegelIdeDbContext er snake_case (`begrepskategori`, `ref_id`, …). Primærnøkkelen har
            // ingen HasColumnName og heter derfor fortsatt "Id" med stor I i basen. Uten
            // anførselstegnene bretter Postgres den ned til `id`, og migrasjonen feiler med
            // «42703: column b.id does not exist» ved oppstart.
            migrationBuilder.Sql("""
                UPDATE tekst_tagger AS t
                SET kind = 'virksomhet'
                WHERE t.kind = 'begrep'
                  AND t.ref_id IS NOT NULL
                  AND EXISTS (
                      SELECT 1 FROM begreper AS b
                      WHERE b."Id" = t.ref_id AND b.begrepskategori = 'virksomhet'
                  );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Bevisst tom — se klassekommentaren.
        }
    }
}
