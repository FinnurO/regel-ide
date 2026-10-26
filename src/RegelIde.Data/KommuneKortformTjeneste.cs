using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>[Ny, issue #390 AK 2] En kortform som har samme tekst som en annen node (område, aktør, navneform eller begrep). Referenten
/// avgjøres av en binding eller av konteksten — aldri her.</summary>
public sealed record KortformKollisjon(string Kommunenummer, string Kortform, string Art, Guid NodeId, string Navn);

/// <summary>[Ny, issue #390 AK 2] En kommune med parallellnavn (samisk, kvensk): kortformen dekker bare det norske navnet fra registeret.</summary>
public sealed record KortformParallellnavn(string Kommunenummer, string Kortform, IReadOnlyList<string> Parallellnavn);

/// <summary>[Ny, issue #390 AK 2] Resultatet av forslagsrunden. <see cref="Nye"/> er forslagene (skrevet når <see cref="Skrevet"/>),
/// <see cref="Fantes"/> kommuner som alt har kortformen, <see cref="AvvistFor"/> kommuner der et menneske har avvist den.</summary>
public sealed record KortformResultat(
    bool Skrevet, int Kommuner, int Nye, int Fantes, int AvvistFor, IReadOnlyList<string> NyeKortformer,
    IReadOnlyList<KortformKollisjon> Kollisjoner, IReadOnlyList<KortformParallellnavn> Parallellnavn, IReadOnlyList<string> Hull);

/// <summary>
/// [Ny, issue #390 AK 2, Johann 2026-10-16] <b>Kortformen for hver kommune</b>: kommunenavnet uten «kommune» («Alta» for Alta kommune), som
/// navneform med grunn <c>kortform</c> og registeret som kilde. Ordet «Alta» betyr området i forskriften om rettskretser og kommunen i andre
/// rettskilder; hvilken referent det har, avgjøres av en binding (#378) eller konteksten, aldri her.
/// <para>
/// <b>Regelen.</b> For hver kommune med territoriekant (<see cref="OmraderegisterSeed"/> steg 2: Enhetsregisteret KOMM ↔ kommunenummer) er
/// kortformen områdets navn i Kartverkets inndeling (<c>kommunenavnNorsk</c>) — teksten kommer fra registeret, den lages ikke ved å kutte
/// ord av registernavnet (CLAUDE.md §8: «Statsforvalteren i innlandet»). Kunnskapsstatus <c>eksternt_dokumentert</c> (registeret er kilden),
/// godkjenning <c>maskinelt_foreslatt</c>: forslag, ikke godkjent. Oppdagelseskilde <see cref="OppdagelsesKilde"/>.
/// </para>
/// <para>
/// <b>Hoppes over:</b> kommunen har alt en gjeldende navneform med samme tekst (stor/liten bokstav likegyldig), eller et menneske har avvist
/// en slik (respekteres, foreslås ikke på nytt). <b>Kollisjoner</b> listes og stoppes ikke: to kommuner med samme navn (Herøy, Våler) får
/// hver sin kortform, og oppslaget blir flertydig til en binding eller konteksten avgjør. Kommunens eget territorium (området «Alta») er
/// ikke en kollisjon — det er nettopp skillet bindingen finnes for. Kortformer for samiske og kvenske parallellnavn foreslås ikke:
/// registeret gir bare det norske navnet, og de listes.
/// </para>
/// <para>Taggene røres ikke: en kortform tagges ikke alene i andre rettskilder (docs/33 §5.11).</para>
/// </summary>
public sealed class KommuneKortformTjeneste(RegelIdeDbContext db)
{
    public const string OppdagelsesKilde = "register:kartverket-kommunenavn";

    /// <param name="kommunenumre">Bare disse kommunene (null = alle). Kollisjonene sjekkes uansett mot hele basen.</param>
    public async Task<KortformResultat> ForeslaAsync(bool skriv, string bruker, IReadOnlyCollection<string>? kommunenumre = null, CancellationToken ct = default)
    {
        // Territoriekantene: kommunen (rettssubjektet) → kommuneområdet, kilde register (Enhetsregisteret + Kartverket).
        var territorier = await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Ansvarsomrade && k.Typekode == "har_ansvarsomrade" && k.FraVirksomhetId != null
                        && k.TilBegrepId != null && k.KildeUtenforKorpusType == Strukturkanter.Register && k.Entitetsstatus == Strukturkanter.Gjeldende)
            .Join(db.Begreper.Where(b => b.Omradetype == Omradetyper.Kommune && b.Omradekode != null && b.Entitetsstatus == "gjeldende" && b.GyldigTil == null),
                k => k.TilBegrepId, b => b.Id, (k, b) => new { Virksomhet = k.FraVirksomhetId!.Value, Omrade = b.Id, b.Term, Kode = b.Omradekode!, k.KildeUtenforKorpusLenke })
            .Join(db.Virksomheter, x => x.Virksomhet, v => v.Id, (x, v) => new { x.Virksomhet, x.Omrade, x.Term, x.Kode, v.Kommunenummer, v.Organisasjonsnummer, x.KildeUtenforKorpusLenke })
            .ToListAsync(ct);
        var hull = new List<string>();
        var entydige = new List<(Guid Virksomhet, Guid Omrade, string Term, string Kode, string? Orgnr)>();
        foreach (var g in territorier.Where(x => x.Kommunenummer == x.Kode && (kommunenumre == null || kommunenumre.Contains(x.Kode))).GroupBy(x => x.Virksomhet))
        {
            var omr = g.Select(x => x.Omrade).Distinct().ToList();
            if (omr.Count != 1) { hull.Add($"{g.First().Kode}: {omr.Count} territorier for samme aktør — ikke entydig, ingen kortform."); continue; }
            var x = g.First();
            entydige.Add((x.Virksomhet, x.Omrade, x.Term, x.Kode, x.Organisasjonsnummer));
        }
        var dupliserteOmrader = entydige.GroupBy(x => x.Omrade).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        foreach (var o in dupliserteOmrader) hull.Add($"{entydige.First(x => x.Omrade == o).Kode}: flere aktører med samme territorium — ingen kortform.");
        entydige = entydige.Where(x => !dupliserteOmrader.Contains(x.Omrade)).OrderBy(x => x.Kode, StringComparer.Ordinal).ToList();

        var virksomhetIder = entydige.Select(x => x.Virksomhet).ToList();
        var navneformer = await db.Begreper.IgnoreQueryFilters()
            .Where(b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId != null && virksomhetIder.Contains(b.VirksomhetReferanseId.Value))
            .Select(b => new { b.Id, Virksomhet = b.VirksomhetReferanseId!.Value, b.Term, b.Navneformgrunn, b.Godkjenning, b.Entitetsstatus })
            .ToListAsync(ct);

        var nye = new List<(Guid Virksomhet, Guid Omrade, string Term, string Kode, string? Orgnr)>();
        int fantes = 0, avvistFor = 0;
        foreach (var x in entydige)
        {
            var samme = navneformer.Where(n => n.Virksomhet == x.Virksomhet && string.Equals(n.Term, x.Term, StringComparison.OrdinalIgnoreCase)).ToList();
            if (samme.Any(n => n.Entitetsstatus == "gjeldende")) { fantes++; continue; }
            if (samme.Any(n => n.Godkjenning == Strukturkanter.Avvist)) { avvistFor++; continue; }
            nye.Add(x);
        }

        // Kollisjoner: alle kortformene (nye og de som fantes) mot andre noder med samme tekst.
        var kortformer = entydige.Select(x => (x.Kode, x.Term, x.Virksomhet, x.Omrade)).ToList();
        var termer = kortformer.Select(k => k.Term.ToLower()).Distinct().ToList();
        var begreper = await db.Begreper
            .Where(b => b.Entitetsstatus == "gjeldende" && termer.Contains(b.Term.ToLower()))
            .Select(b => new { b.Id, b.Term, b.Begrepskategori, b.Omradetype, b.Omradekode, b.VirksomhetReferanseId, b.Navneformgrunn })
            .ToListAsync(ct);
        var virksomheter = await db.Virksomheter.Where(v => termer.Contains(v.Navn.ToLower())).Select(v => new { v.Id, v.Navn }).ToListAsync(ct);
        var refIder = begreper.Where(b => b.VirksomhetReferanseId != null).Select(b => b.VirksomhetReferanseId!.Value).Distinct().ToList();
        var refNavn = await db.Virksomheter.Where(v => refIder.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.Navn, ct);
        var kollisjoner = new List<KortformKollisjon>();
        foreach (var (kode, term, virksomhet, omrade) in kortformer)
        {
            var t = term.ToLower();
            foreach (var b in begreper.Where(b => b.Term.ToLower() == t && b.Id != omrade && b.VirksomhetReferanseId != virksomhet))
            {
                var art = b.Begrepskategori == Nodetyper.Omrade ? $"område ({b.Omradetype}{(b.Omradekode is null ? "" : " " + b.Omradekode)})"
                    : b.VirksomhetReferanseId is { } r ? $"navneform ({b.Navneformgrunn ?? "uten grunn"}) for {refNavn.GetValueOrDefault(r) ?? r.ToString()}"
                    : $"begrep ({b.Begrepskategori ?? "uten kategori"})";
                kollisjoner.Add(new(kode, term, art, b.Id, b.Term));
            }
            // En annen kommune med samme kortform (begge nye — finnes ikke som rad ennå).
            foreach (var annen in nye.Where(n => n.Virksomhet != virksomhet && n.Term.ToLower() == t))
            {
                kollisjoner.Add(new(kode, term, $"kortform (forslag i samme runde) for kommune {annen.Kode}", annen.Virksomhet, annen.Term));
            }
            foreach (var v in virksomheter.Where(v => v.Navn.ToLower() == t && v.Id != virksomhet))
            {
                kollisjoner.Add(new(kode, term, "aktør (registernavn)", v.Id, v.Navn));
            }
        }

        var parallell = navneformer.Where(n => n.Navneformgrunn == "parallellnavn" && n.Entitetsstatus == "gjeldende")
            .GroupBy(n => n.Virksomhet)
            .Select(g => { var x = entydige.First(e => e.Virksomhet == g.Key); return new KortformParallellnavn(x.Kode, x.Term, g.Select(n => n.Term).OrderBy(s => s, StringComparer.Ordinal).ToList()); })
            .OrderBy(p => p.Kommunenummer, StringComparer.Ordinal).ToList();

        if (skriv)
        {
            foreach (var x in nye)
            {
                var b = new BegrepEntitet
                {
                    Id = Guid.NewGuid(),
                    Begrepskategori = "virksomhet",
                    VirksomhetReferanseId = x.Virksomhet,
                    Navneformgrunn = "kortform",
                    Term = x.Term,
                    Status = "publisert",
                    Kunnskapsstatus = Strukturkanter.EksterntDokumentert,
                    KildeUtenforKorpusType = Strukturkanter.Register,
                    KildeUtenforKorpusDokumentasjon = Strukturkanter.Primaer,
                    KildeUtenforKorpusTekst = $"Kartverket kommuneinfo: kommunenavnNorsk «{x.Term}» for kommune {x.Kode} (øyeblikksbildet i Seed/kartverket-fylker-kommuner.json, hentet 2026-10-08); "
                        + $"Enhetsregisteret: organisasjonsform KOMM {x.Orgnr} har kommunenummer {x.Kode}",
                    KildeUtenforKorpusLenker = ["https://ws.geonorge.no/kommuneinfo/v1/fylkerkommuner",
                        .. x.Orgnr is null ? Array.Empty<string>() : [$"https://data.brreg.no/enhetsregisteret/api/enheter/{x.Orgnr}"]],
                    Begrunnelse = "Kortform: kommunenavnet uten «kommune», slik registeret skriver det (#390). Hva ordet viser til i en rettskilde "
                        + "(kommunen eller området), avgjøres av en binding eller konteksten.",
                    Godkjenning = Strukturkanter.MaskineltForeslatt,
                    OppdagelsesKilde = OppdagelsesKilde,
                    OpprettetAv = bruker,
                    OpprettetTidspunkt = DateTimeOffset.UtcNow,
                };
                db.Begreper.Add(b);
                db.Proveniens.Add(ProveniensHjelper.NyRad("begrep", b.Id, virksomhetId: null, "foreslatt", bruker));
            }
            await db.SaveChangesAsync(ct);
        }
        return new KortformResultat(skriv, entydige.Count, nye.Count, fantes, avvistFor,
            nye.Select(n => $"{n.Kode} {n.Term}").ToList(),
            kollisjoner.DistinctBy(k => (k.Kommunenummer, k.NodeId)).OrderBy(k => k.Kommunenummer, StringComparer.Ordinal).ToList(), parallell, hull);
    }
}
