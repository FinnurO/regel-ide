using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, konfidens-runden, 2026-09-09] To nye, nullbare kolonner PLUSS en datamigrasjon som henter
    /// de automatisk avviste radene tilbake i køen.
    ///
    /// <para>
    /// Et <c>'virksomhet'</c>-treff som SNL/SSR ikke bekreftet fikk <c>Status = 'Avvist'</c> direkte
    /// fra sveipet. Det skjulte reelle organer: «Reguleringsmyndigheten» og «Energiklagenemndas» lå
    /// begge som automatisk avvist i forskrift om Energiklagenemnda, fordi SNL ikke har artikler om
    /// dem. Johann 2026-09-09: «vi kan ikke automatisk avvise disse p.g.a. manglende SNL/SSR. Kan vi
    /// innføre Høy/Lav konfidens fremfor å avvise dem?»
    /// </para>
    ///
    /// <para>
    /// <b>Datamigrasjonen</b> treffer nøyaktig signaturen til auto-avvisningen: kategori
    /// <c>'virksomhet'</c>, <c>status = 'Avvist'</c> og <c>behandlet_av IS NULL</c> — altså avvist
    /// uten at noe menneske har rørt raden. De settes tilbake til <c>'Venter'</c> med
    /// <c>konfidens = 'lav'</c>. Rader et menneske faktisk har avvist (<c>behandlet_av</c> satt) står
    /// urørt: det er en avgjørelse, ikke en systemantakelse.
    /// </para>
    ///
    /// <para>
    /// <c>konfidens_grunn</c> settes bevisst IKKE for disse radene. Grunnen ble ikke lagret da de ble
    /// avvist, og kan ikke utledes i ettertid — «ukjent i SNL og SSR» og «SSR-bekreftet stedsnavn uten
    /// institusjonsord etter» er to helt ulike vurderinger, og å gjette den ene ville vært verre enn å
    /// la feltet stå tomt. UI-et viser derfor «grunn ikke registrert» for dem. Et nytt sveip vil sette
    /// grunnen for nye rader.
    /// </para>
    ///
    /// <para>
    /// <c>Down</c> dropper kolonnene (det EF genererte), men gjenoppretter IKKE statusene: etter
    /// <c>Up</c> er de tilbakestilte radene ikke skillbare fra rader som alltid har ventet, og en
    /// reversering ville måttet gjette hvilke som skulle avvises igjen.
    /// </para>
    /// </summary>
    public partial class LeggTilKonfidensPaNavnekandidat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "konfidens",
                table: "navnekandidater",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "konfidens_grunn",
                table: "navnekandidater",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_navnekandidater_konfidens",
                table: "navnekandidater",
                sql: "konfidens IS NULL OR konfidens IN ('hoy', 'lav')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_navnekandidater_konfidens_grunn",
                table: "navnekandidater",
                sql: "konfidens_grunn IS NULL OR konfidens_grunn IN ('snl_treff', 'ssr_med_institusjonsord', 'ssr_uten_institusjonsord', 'ukjent_i_snl_og_ssr')");

            // Datamigrasjonen — se klassekommentaren for avgrensningen og for hvorfor
            // konfidens_grunn står tom.
            migrationBuilder.Sql("""
                UPDATE navnekandidater
                SET status = 'Venter', konfidens = 'lav'
                WHERE kategori = 'virksomhet'
                  AND status = 'Avvist'
                  AND behandlet_av IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_navnekandidater_konfidens",
                table: "navnekandidater");

            migrationBuilder.DropCheckConstraint(
                name: "ck_navnekandidater_konfidens_grunn",
                table: "navnekandidater");

            migrationBuilder.DropColumn(
                name: "konfidens",
                table: "navnekandidater");

            migrationBuilder.DropColumn(
                name: "konfidens_grunn",
                table: "navnekandidater");
        }
    }
}
