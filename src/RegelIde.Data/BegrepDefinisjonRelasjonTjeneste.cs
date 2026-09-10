using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, #212, 2026-09-10] Deteksjon + kø + bekreftelse for «definert likt som»-relasjoner mellom
/// begreps-forekomster/-rader — se <see cref="BegrepDefinisjonRelasjonKandidatEntitet"/> og
/// <see cref="BegrepDefinisjonRelasjonEntitet"/> i Entiteter.cs for hele det bindende resonnementet
/// (Kjernebeslutningen fra issue #212, besluttet med Johann 2026-09-09/2026-09-10).
/// <para>
/// <b>Målt mot ekte data, 2026-09-10 (direkte SQL mot den kjørende dev-databasen, ikke embedded-test-data):</b>
/// M11-sveipet har 170 forekomster, 136 unike termer, 10 termer i mer enn én rettskilde — reproduserer
/// EKSAKT issue #212 sin manuelt fastslåtte tabell (samme 10 termer, samme antall rettskilder per term).
/// Deteksjonsregelen i DENNE klassen (eksakt likhet etter normalisering, IKKE ren term-likhet) treffer
/// STRENGERE enn term-tabellen over: 10 forekomst-grupper (25 forekomster, 6 av de 10 kjente termene —
/// "fellesgrader"×3 undergrupper, "cotutelle-avtaler"×2, "cotutelle", "inntektsbortfall", "uunngåelige
/// økonomiske forpliktelser", "omsorgsyter"). De resterende 4 kjente termene ("privatist", "rederiet",
/// "studierett", "maritimt sikringsnivå") har INGEN forekomst-par som er eksakt like etter normalisering
/// — hver av dem har minst ett ekte ordlyd-avvik, ikke bare tegnsetting, og foreslås derfor bevisst IKKE
/// automatisk (Johanns eksplisitte regel, se <see cref="NormaliserDefinisjon"/>). Se PR-beskrivelsen for
/// full utskrift av målingen.
/// </para>
/// </summary>
public sealed class BegrepDefinisjonRelasjonTjeneste(RegelIdeDbContext db)
{
    /// <summary>
    /// Johanns deteksjonsregel, verbatim (kommentar på issue #212, 2026-09-10): «eksakt lik etter
    /// normalisering. To definisjoner regnes som samme definisjon når teksten er identisk etter at
    /// mellomrom, store/små bokstaver og tegnsetting er normalisert bort. Deterministisk, ingen terskel
    /// å tune, ingen falske positive — og to definisjoner som skiller seg med ett ord regnes som ULIKE,
    /// altså ikke relatert automatisk.» Dette ERSTATTER en tidligere skissert ord-for-ord-scoring (issuets
    /// opprinnelige tekst) — se klassekommentaren.
    /// <para>
    /// Konkret: lavercaser (<see cref="char.ToLowerInvariant(char)"/>), beholder bokstaver/siffer, dropper
    /// ALL annen tegnsetting HELT (ikke erstattet med mellomrom — "cotutelle-avtaler" blir
    /// "cotutelleavtaler", ikke "cotutelle avtaler"), og kollapser sammenhengende mellomrom/tab/linjeskift
    /// til ETT mellomrom.
    /// </para>
    /// <para>
    /// [Ny, #212, 2026-09-10] Strippes FØRST for et ledende leddnummer («(1) Med fellesgrad menes …») —
    /// issuets eget «Sidefunn»: leddnummeret har lekket inn i <see cref="RettskildeNodeEntitet.Tekst"/> ved
    /// import for noen dokumenter (33fe3a3c, 56948b1f, f63fab04) og ødelegger ellers sammenligningen for
    /// disse tre. Billig, deterministisk, og løser nøyaktig det problemet issuet selv flagger som verdt å
    /// rette — målt: uten denne strippingen mister "fellesgrader"-gruppen én forekomst (5 i stedet for 6
    /// av 16). Retter IKKE selve importfeilen (den STÅR fortsatt i <c>Tekst</c>) — kun i sammenligningen
    /// her, se issuets «Sidefunn» for hvorfor selve importretting er en egen vurdering.
    /// </para>
    /// </summary>
    public static string NormaliserDefinisjon(string tekst)
    {
        var utenLeddnummer = LeddnummerPrefiksMønster.Replace(tekst, "");
        var sb = new StringBuilder(utenLeddnummer.Length);
        var forrigeVarMellomrom = false;
        foreach (var c in utenLeddnummer.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                forrigeVarMellomrom = false;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (!forrigeVarMellomrom)
                {
                    sb.Append(' ');
                    forrigeVarMellomrom = true;
                }
            }
            // all annen tegnsetting (komma, bindestrek, parenteser, punktum …) droppes helt.
        }
        return sb.ToString().Trim();
    }

    private static readonly Regex LeddnummerPrefiksMønster = new(@"^\(\d+\)\s*");

    /// <summary>Sveiper alle <see cref="BegrepsforekomstEntitet"/>-rader med satt <see cref="BegrepsforekomstEntitet.Definisjon"/>
    /// (per i dag M1 og M11, se BegrepsoppdagelseSveipTjeneste — IKKE begrenset til M11 her: mønsteret er
    /// identisk uansett hvilket sveipmønster som fant definisjonsteksten), grupperer på
    /// <see cref="NormaliserDefinisjon"/>, og oppretter ett kandidatpar PER PAR innenfor hver gruppe som
    /// spenner over MER ENN ÉN rettskilde (samme term/definisjon flere steder i SAMME forskrift er ikke
    /// interessant — det er duplikatimport, ikke en tverr-forskrift-kobling). Idempotent (AC4) via
    /// <see cref="OpprettEllerFinnKandidatAsync"/>.</summary>
    /// <param name="opprettetAv">Attribusjon på nye kandidatrader.</param>
    /// <param name="rettskildeIder">Valgfri innsnevring til et gitt sett rettskilder (samme
    /// "null = hele korpuset, satt = et delsett" -mønster som <c>BegrepsoppdagelseSveipTjeneste.SveipAsync</c>s
    /// <c>rettskildeId</c>, men her en MENGDE siden deteksjon per definisjon spenner over MINST to
    /// rettskilder). Praktisk for et målrettet resveip etter at noen få rettskilder er reimportert, OG
    /// nødvendig for testisolasjon mot delt embedded Postgres (se testfilens kommentar).</param>
    public async Task<BegrepDefinisjonRelasjonSveipResultat> SveipAsync(
        string opprettetAv, IReadOnlyCollection<Guid>? rettskildeIder = null, CancellationToken ct = default)
    {
        var spørring = db.Begrepsforekomster.Where(f => f.Definisjon != null);
        if (rettskildeIder is not null) spørring = spørring.Where(f => rettskildeIder.Contains(f.RettskildeId));
        var forekomster = await spørring
            .Select(f => new { f.Id, f.RettskildeId, f.Definisjon })
            .ToListAsync(ct);

        var grupper = forekomster
            .GroupBy(f => NormaliserDefinisjon(f.Definisjon!))
            .Where(g => g.Select(x => x.RettskildeId).Distinct().Count() > 1)
            .ToList();

        var antallNyeKandidater = 0;
        foreach (var gruppe in grupper)
        {
            var rader = gruppe.ToList();
            // Alle par INNENFOR gruppen, ikke bare naboer — en gruppe på N forekomster gir C(N,2) par.
            // Grupper er i praksis små (målt maks 6 av 170 M11-forekomster, 2026-09-10) — ingen
            // skaleringsbekymring på dette datavolumet.
            for (var i = 0; i < rader.Count; i++)
            {
                for (var j = i + 1; j < rader.Count; j++)
                {
                    // Kun interessant på tvers av rettskilder, se metodekommentaren.
                    if (rader[i].RettskildeId == rader[j].RettskildeId) continue;
                    var (ny, _) = await OpprettEllerFinnKandidatAsync(
                        rader[i].Id, rader[j].Id, gruppe.Key, opprettetAv, ct);
                    if (ny) antallNyeKandidater++;
                }
            }
        }

        return new BegrepDefinisjonRelasjonSveipResultat(grupper.Count, antallNyeKandidater);
    }

    /// <summary>Idempotent — canonicaliserer paret (<c>FraForekomstId &lt; TilForekomstId</c>) FØR oppslag/
    /// innsetting, slik at samme par funnet i motsatt rekkefølge (f.eks. et fremtidig sveip som itererer i
    /// annen rekkefølge) gir samme rad tilbake, ikke et duplikat — samme "racy sveip"-vern
    /// (<see cref="DbUpdateException"/>-fallback) som <see cref="NavnekandidatOppdagelseTjeneste.OpprettEllerFinnAsync"/>.</summary>
    public async Task<(bool NyRad, BegrepDefinisjonRelasjonKandidatEntitet Kandidat)> OpprettEllerFinnKandidatAsync(
        Guid forekomstId1, Guid forekomstId2, string normalisertDefinisjon, string opprettetAv, CancellationToken ct = default)
    {
        var (fraId, tilId) = forekomstId1.CompareTo(forekomstId2) < 0
            ? (forekomstId1, forekomstId2) : (forekomstId2, forekomstId1);

        var eksisterende = await db.BegrepDefinisjonRelasjonKandidater
            .FirstOrDefaultAsync(k => k.FraForekomstId == fraId && k.TilForekomstId == tilId, ct);
        if (eksisterende is not null) return (false, eksisterende);

        var kandidat = new BegrepDefinisjonRelasjonKandidatEntitet
        {
            Id = Guid.NewGuid(),
            FraForekomstId = fraId,
            TilForekomstId = tilId,
            NormalisertDefinisjon = normalisertDefinisjon,
            Status = "Venter",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.BegrepDefinisjonRelasjonKandidater.Add(kandidat);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.Entry(kandidat).State = EntityState.Detached;
            var vantLopet = await db.BegrepDefinisjonRelasjonKandidater
                .FirstOrDefaultAsync(k => k.FraForekomstId == fraId && k.TilForekomstId == tilId, ct);
            if (vantLopet is not null) return (false, vantLopet);
            throw;
        }
        return (true, kandidat);
    }

    /// <summary>Kun <c>'Venter'</c>-rader — se <see cref="ListerAsync"/> for full liste.</summary>
    public Task<List<BegrepDefinisjonRelasjonKandidatEntitet>> ListerVentendeAsync(CancellationToken ct = default) =>
        ListerAsync("Venter", ct);

    /// <summary><paramref name="status"/> = <c>null</c> betyr ALLE statuser (samme eksplisitte
    /// "ingen stille standard"-mønster som <see cref="BegrepsforekomstTjeneste.ListerAsync"/>).</summary>
    public Task<List<BegrepDefinisjonRelasjonKandidatEntitet>> ListerAsync(string? status = null, CancellationToken ct = default)
    {
        var spørring = db.BegrepDefinisjonRelasjonKandidater.AsQueryable();
        if (status is not null) spørring = spørring.Where(k => k.Status == status);
        return spørring.OrderByDescending(k => k.OpprettetTidspunkt).ToListAsync(ct);
    }

    /// <summary>
    /// Bekrefter kandidaten: krever at BEGGE underliggende forekomster allerede er godkjent til et
    /// <see cref="BegrepEntitet"/> (<see cref="BegrepsforekomstEntitet.BegrepId"/> satt) — Kjernebeslutningen
    /// på #212 er eksplisitt om en relasjon mellom <see cref="BegrepEntitet"/>-rader, og HVILKEN
    /// virksomhets register en uapprovert forekomst skal landes i er ikke noe denne metoden kan gjette
    /// (§8, samme linje som <see cref="BegrepsforekomstTjeneste.GodkjennAsync"/> krever et eksplisitt
    /// <c>virksomhetId</c>-valg av mennesket, ikke en utledning). Godkjenn de to forekomstene i
    /// begrepsforekomst-køen FØRST, bekreft relasjonen deretter.
    /// <para>
    /// Oppretter <see cref="BegrepDefinisjonRelasjonEntitet"/> for BEGGE retninger, idempotent (AC4) —
    /// sjekker eksisterende rad PER retning før innsetting, så et gjentatt kall (eller et nytt sveip som
    /// finner samme par via en annen forekomst-kombinasjon) ikke dupliserer.
    /// </para>
    /// </summary>
    public async Task<BegrepDefinisjonRelasjonKandidatEntitet?> GodkjennAsync(
        Guid id, string behandletAv, CancellationToken ct = default)
    {
        var kandidat = await db.BegrepDefinisjonRelasjonKandidater.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kandidat is null) return null;
        if (kandidat.Status != "Venter")
        {
            throw new ArgumentException(
                $"Kandidaten har status '{kandidat.Status}' — kan kun godkjenne kandidater med status 'Venter'.");
        }

        var fraForekomst = await db.Begrepsforekomster.FirstOrDefaultAsync(f => f.Id == kandidat.FraForekomstId, ct);
        var tilForekomst = await db.Begrepsforekomster.FirstOrDefaultAsync(f => f.Id == kandidat.TilForekomstId, ct);
        if (fraForekomst is null || tilForekomst is null)
        {
            throw new ArgumentException("Fant ikke én eller begge de underliggende forekomstene. Ingen gjettet fallback.");
        }
        if (fraForekomst.BegrepId is null || tilForekomst.BegrepId is null)
        {
            throw new ArgumentException(
                "Begge forekomstene må først være godkjent til et Begrep i registeret (POST " +
                "/api/begrepsforekomster/{id}/godkjenn) før relasjonen mellom dem kan bekreftes — " +
                "relasjonen er mellom to Begrep-rader, ikke rå forekomster (issue #212, kjernebeslutningen). " +
                "Ingen gjettet virksomhet å opprette den i.");
        }

        var fraBegrepId = fraForekomst.BegrepId.Value;
        var tilBegrepId = tilForekomst.BegrepId.Value;
        if (fraBegrepId != tilBegrepId)
        {
            await OpprettRelasjonHvisManglerAsync(fraBegrepId, tilBegrepId, behandletAv, ct);
            await OpprettRelasjonHvisManglerAsync(tilBegrepId, fraBegrepId, behandletAv, ct);
        }
        // fraBegrepId == tilBegrepId: begge forekomstene ble godkjent til SAMME Begrep-rad i mellomtiden
        // (f.eks. begge tagget samme term i samme rettskilde) — ingen selvrelasjon å opprette, men
        // kandidaten markeres likevel behandlet under, ikke stående i Venter for alltid.

        kandidat.Status = "Godkjent";
        kandidat.BehandletAv = behandletAv;
        kandidat.BehandletTidspunkt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return kandidat;
    }

    private async Task OpprettRelasjonHvisManglerAsync(Guid fraBegrepId, Guid tilBegrepId, string opprettetAv, CancellationToken ct)
    {
        var finnes = await db.BegrepDefinisjonRelasjoner
            .AnyAsync(r => r.FraBegrepId == fraBegrepId && r.TilBegrepId == tilBegrepId, ct);
        if (finnes) return;
        db.BegrepDefinisjonRelasjoner.Add(new BegrepDefinisjonRelasjonEntitet
        {
            Id = Guid.NewGuid(),
            FraBegrepId = fraBegrepId,
            TilBegrepId = tilBegrepId,
            Kilde = "sveip",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
    }

    public async Task<BegrepDefinisjonRelasjonKandidatEntitet?> AvvisAsync(Guid id, string behandletAv, CancellationToken ct = default)
    {
        var kandidat = await db.BegrepDefinisjonRelasjonKandidater.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kandidat is null) return null;
        if (kandidat.Status != "Venter")
        {
            throw new ArgumentException(
                $"Kandidaten har status '{kandidat.Status}' — kan kun avvise kandidater med status 'Venter'.");
        }
        kandidat.Status = "Avvist";
        kandidat.BehandletAv = behandletAv;
        kandidat.BehandletTidspunkt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return kandidat;
    }

    /// <summary>Samme som <see cref="ListerAsync"/>, men join'er inn de to underliggende forekomstene
    /// (én ekstra spørring, batch-lastet — ikke N+1) — dekker AC2/AC3 på #212: klienten trenger
    /// forekomstenes rettskilde/tekst for å vise «hvilke andre rettskilder» og selve ordlyden uten et
    /// separat kall per rad.</summary>
    public async Task<List<(BegrepDefinisjonRelasjonKandidatEntitet Kandidat, BegrepsforekomstEntitet Fra, BegrepsforekomstEntitet Til)>>
        ListerMedForekomsterAsync(string? status = null, CancellationToken ct = default)
    {
        var kandidater = await ListerAsync(status, ct);
        var forekomstIder = kandidater.SelectMany(k => new[] { k.FraForekomstId, k.TilForekomstId }).Distinct().ToList();
        var forekomster = await db.Begrepsforekomster
            .Where(f => forekomstIder.Contains(f.Id)).ToDictionaryAsync(f => f.Id, ct);
        return kandidater.Select(k => (k, forekomster[k.FraForekomstId], forekomster[k.TilForekomstId])).ToList();
    }

    /// <summary>Alle bekreftede relasjoner FRA det gitte begrepet — brukt av BegrepDetalj (AC5, «også
    /// definert i N andre rettskilder»). Symmetrisk lagring (se entitetskommentaren) gjør dette til et
    /// rent <c>WHERE fra_begrep_id = @id</c>-oppslag, uansett hvilken retning relasjonen opprinnelig ble
    /// bekreftet i.</summary>
    public Task<List<BegrepDefinisjonRelasjonEntitet>> ListerRelasjonerForBegrepAsync(Guid begrepId, CancellationToken ct = default) =>
        db.BegrepDefinisjonRelasjoner.Where(r => r.FraBegrepId == begrepId).ToListAsync(ct);
}

public sealed record BegrepDefinisjonRelasjonSveipResultat(int AntallGrupperFunnet, int AntallNyeKandidater);
