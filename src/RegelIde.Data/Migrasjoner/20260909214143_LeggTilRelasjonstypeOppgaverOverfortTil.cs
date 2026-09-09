using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, etterfølgelse-runden, 2026-09-09, issue #134] REN DATAMIGRASJON — ingen skjemaendring.
    ///
    /// <para>
    /// Legger relasjonstypen <c>oppgaver_overfort_til</c> inn i
    /// <see cref="RelasjonsTypeKonfigurasjonEntitet"/>. Typen er også lagt til i oppstartsseeden i
    /// <c>Program.cs</c>, men den seeden kjører BARE når tabellen er tom (samme «seed hvis tom»-
    /// mønster som tagg-kindene). En base som allerede har de fire opprinnelige typene ville derfor
    /// aldri fått den femte. Derav denne migrasjonen.
    /// </para>
    ///
    /// <para>
    /// <b>Hvorfor typen trengs:</b> de fire eksisterende typene (<c>underlagt</c>,
    /// <c>sekretariat</c>, <c>klageinstans</c>, <c>enhet_i</c>) beskriver alle organer som
    /// eksisterer SAMTIDIG. Ingen av dem uttrykker rettslig etterfølgelse — at et organ er avviklet
    /// og oppgavene overtatt av et annet. Advokatloven § 73 gjør nettopp det, og splitter til og med
    /// Advokatbevillingsnemndens saker mellom to etterfølgere: klagesaker til Advokatnemnda
    /// (syvende ledd), alle andre saker til Advokattilsynet (åttende ledd).
    /// </para>
    ///
    /// <para>
    /// Idempotent: <c>WHERE NOT EXISTS</c> gjør at migrasjonen er trygg selv om oppstartsseeden
    /// allerede har lagt inn raden i en fersk base (rekkefølgen mellom migrasjon og seed er ikke noe
    /// vi vil være avhengige av).
    /// </para>
    ///
    /// <para>
    /// <c>Down</c> fjerner typen igjen, men KUN hvis ingen relasjon bruker den — å slette en type
    /// som er i bruk ville etterlatt rader med en <c>RelasjonsType</c> uten visningsmal, og de ville
    /// vist «(ukjent relasjonstype)» i UI-et. Se <c>VirksomhetRelasjonregisterTjeneste</c>.
    /// </para>
    /// </summary>
    public partial class LeggTilRelasjonstypeOppgaverOverfortTil : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // «"Id"» med anførselstegn: bare de eksplisitt mappede kolonnene er snake_case,
            // primærnøkkelen heter fortsatt "Id" i basen (samme felle som tidligere migrasjoner).
            migrationBuilder.Sql("""
                INSERT INTO relasjonstype_konfigurasjon
                    ("Id", kode, fra_visningsmal, til_visningsmal, sorteringsrekkefolge, aktiv)
                SELECT gen_random_uuid(), 'oppgaver_overfort_til',
                       'fikk oppgavene overført til {0}', 'overtok oppgavene til {0}', 4, true
                WHERE NOT EXISTS (
                    SELECT 1 FROM relasjonstype_konfigurasjon WHERE kode = 'oppgaver_overfort_til'
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM relasjonstype_konfigurasjon AS k
                WHERE k.kode = 'oppgaver_overfort_til'
                  AND NOT EXISTS (
                      SELECT 1 FROM virksomhet_relasjoner AS r
                      WHERE r.relasjons_type = 'oppgaver_overfort_til'
                  );
                """);
        }
    }
}
