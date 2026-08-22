using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Sveipefunksjonen (docs/20 §5, kravspek §4.2 pkt. 1) — tekstsøk gjennom ALLE rettskilde-noder etter
/// forekomster av navneform-<see cref="BegrepEntitet"/>-strenger for én virksomhet
/// (<c>Begrepskategori = "virksomhet"</c>, gruppert på <see cref="BegrepEntitet.VirksomhetReferanseId"/>
/// — IKKE bare <see cref="Virksomhet.Navn"/>, se docs/20 §2.3: synonymer som "Fylkesmann"/"Statsforvalter"
/// er egne <see cref="BegrepEntitet"/>-rader mot samme virksomhet). Hvert treff legges i
/// <see cref="VirksomhetKandidatTjeneste"/>s kø via <see cref="VirksomhetKandidatTjeneste.OpprettEllerFinnAsync"/>,
/// som er idempotent PER (virksomhet, rettskilde, node, start-posisjon).
/// <para>
/// Egen klasse, separat fra <see cref="VirksomhetKandidatTjeneste"/> (selve køen) — se den klassens
/// kommentar.
/// </para>
/// </summary>
public sealed class VirksomhetKandidatSveipTjeneste(RegelIdeDbContext db, VirksomhetKandidatTjeneste kandidatkø)
{
    /// <summary>
    /// Kjører sveipet for én virksomhet mot ALLE rettskilde-noder som har tekst og ikke er opphevet.
    /// <para>
    /// <b>Matching:</b> hver navneform matches med ordgrense (<c>\b</c>) i noden sin <c>Tekst</c> — enkel
    /// regex, som spesifisert i oppgaveteksten. Navneformene sorteres på LENGDE (lengst først) før de
    /// settes sammen i én alternasjons-regex, slik at en lengre navneform som inneholder en kortere som
    /// delstreng ikke risikerer å bare gi et kortere delvis treff (ingen kjente slike par for
    /// Advokattilsynet i dag, men prinsippet holder generelt).
    /// <para>
    /// <b>Case-INSENSITIVT (2026-08-22, Johanns eksplisitte instruks — omgjør en tidligere case-
    /// sensitiv versjon):</b> opprinnelig antagelse var at navneformer alltid er egennavn ("Advokattilsynet"),
    /// men et reelt funn samme dag (Agder fylkeskommune, navneform "Fylkeskommune") viste at ikke alle
    /// navneformer faktisk brukes konsekvent som proper noun i lovtekst — "fylkeskommune" forekommer
    /// nesten utelukkende med liten forbokstav som alminnelig substantiv (286 case-insensitive treff i
    /// korpuset, 0 med eksakt "Fylkeskommune"). Case-insensitivitet er en BEVISST avveining: for en
    /// GENERISK navneform som "Fylkeskommune" (ikke spesifikk for ÉN fylkeskommune) vil dette gi mange
    /// falske positiver (alle generelle lovomtaler av fylkeskommuner, ikke bare Agders) — akseptert
    /// fordi kandidatkøen uansett krever manuell godkjenning/avvisning (§2.6) før noe blir en ekte tagg,
    /// så en for bred sveip er en arbeidsbyrde, ikke en korrekthetsfeil. Den REELLE anbefalingen for en
    /// presis navneform er fortsatt å bruke en spesifikk frase ("Agder fylkeskommune") — det løses ikke
    /// av denne kodeendringen, kun gjort MULIG å leve med et bredere/upresist navneform-valg.
    /// </para>
    /// <para>
    /// <b>Ytelse:</b> full in-memory scan av alle rettskilde-noder per sveip — akseptabelt for dagens
    /// datamengde, ikke optimalisert med noe fulltekstindeks. Dokumentert begrensning, ikke en
    /// overraskelse.
    /// </para>
    /// </summary>
    public async Task<VirksomhetKandidatSveipResultat> SveipAsync(Guid virksomhetId, string opprettetAv, CancellationToken ct = default)
    {
        if (!await db.Virksomheter.AnyAsync(v => v.Id == virksomhetId, ct))
        {
            throw new ArgumentException($"Fant ingen virksomhet med id '{virksomhetId}'. Ingen gjettet fallback.");
        }

        var navneformer = await db.Begreper
            .Where(b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == virksomhetId && b.Entitetsstatus == "gjeldende")
            .Select(b => b.Term)
            .ToListAsync(ct);
        if (navneformer.Count == 0)
        {
            throw new ArgumentException(
                $"Virksomheten '{virksomhetId}' har ingen navneform-begrep (Begrepskategori='virksomhet') å søke etter. " +
                "Ingen gjettet fallback — opprett minst én navneform først.");
        }

        var mønster = new Regex(
            @"\b(?:" + string.Join('|', navneformer.OrderByDescending(n => n.Length).Select(Regex.Escape)) + @")\b",
            RegexOptions.IgnoreCase);

        var noder = await db.RettskildeNoder
            .Where(n => n.Tekst != null && !n.Opphevet && n.Entitetsstatus == "gjeldende")
            .Select(n => new { n.RettskildeId, n.Eid, n.Tekst })
            .ToListAsync(ct);

        var antallTreff = 0;
        var antallNyeKandidater = 0;
        foreach (var node in noder)
        {
            foreach (Match treff in mønster.Matches(node.Tekst!))
            {
                antallTreff++;
                var forAntall = await db.VirksomhetKandidater.CountAsync(
                    k => k.VirksomhetId == virksomhetId && k.RettskildeId == node.RettskildeId
                         && k.NodeEid == node.Eid && k.StartOffset == treff.Index, ct);
                await kandidatkø.OpprettEllerFinnAsync(
                    virksomhetId, node.RettskildeId, node.Eid, treff.Index, treff.Index + treff.Length, opprettetAv, ct);
                if (forAntall == 0) antallNyeKandidater++;
            }
        }

        return new VirksomhetKandidatSveipResultat(antallTreff, antallNyeKandidater);
    }
}

/// <summary>Oppsummering av ett sveip-kjøring — <see cref="AntallTreffFunnet"/> teller ALLE forekomster
/// funnet i teksten (også de som allerede fantes som kandidat fra et tidligere sveip),
/// <see cref="AntallNyeKandidater"/> kun de som faktisk ble en NY rad i køen denne kjøringen.</summary>
public sealed record VirksomhetKandidatSveipResultat(int AntallTreffFunnet, int AntallNyeKandidater);
