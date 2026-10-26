using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Strukturkonvertering;

// [Ny, issue #387, Johanns go 2026-10-27] Fasiten inn i appen: utsagnene i data/fasit/strukturmodell lastes som FAGLIG VURDERT (ikke
// godkjent — fasiten er ikke uavhengig kontrollert, docs/profil/03 §10) gjennom samme tjenestevei som forslagskøen (#313): samme
// aktørklassifisering (#378/#385), samme skrivevei (StrukturkantTjeneste.OpprettAsync/ReviderAsync) og samme typeavbildning
// (StrukturforslagTjeneste.KanttypeFor). Opphavet står i oppdagelseskilden: fasit:<fil>:<id>.

/// <summary>[Ny, issue #387] Utfallet for ett fasitutsagn. Lukket liste, så tallene kan sammenlignes mellom kjøringer.</summary>
public static class Fasitutfall
{
    /// <summary>Ny rad, <c>faglig_vurdert</c>.</summary>
    public const string Ny = "ny";
    /// <summary>Raden fra en tidligere import (samme opphav) er revidert fordi fasiten er endret (ny rad, den gamle foreldet).</summary>
    public const string Oppdatert = "oppdatert";
    /// <summary>Raden fra en tidligere import (samme opphav) er lik fasiten — ingenting skrevet.</summary>
    public const string Uendret = "uendret";
    /// <summary>Samme utsagn lå som ventende forslag (mønster, KI, manuelt) — forslaget er revidert til fasitraden (ny rad, forslaget
    /// foreldet, historikken står).</summary>
    public const string ErstattetForslag = "erstattet_forslag";
    /// <summary>Samme utsagn finnes alt som godkjent eller faglig vurdert fra en annen kilde, med samme innhold — ingenting skrevet.</summary>
    public const string FantesVurdert = "fantes_vurdert";
    /// <summary>Samme utsagn finnes som godkjent eller faglig vurdert, men med annet innhold (eller fasitraden er endret etter at et
    /// menneske godkjente den) — ikke overskrevet, listet.</summary>
    public const string Konflikt = "konflikt";
    /// <summary>Samme utsagn (eller samme opphav) er AVVIST — lastes ikke på nytt (#374 AC4, #387 AK 3).</summary>
    public const string TidligereAvvist = "tidligere_avvist";
    /// <summary>Samme utsagn to ganger i samme kjøring — den første vant.</summary>
    public const string DublettIKjoringen = "dublett_i_kjoringen";
    /// <summary>Ikke lastet — se årsaken.</summary>
    public const string IkkeLastet = "ikke_lastet";

    public static readonly string[] Alle = [Ny, Oppdatert, Uendret, ErstattetForslag, FantesVurdert, Konflikt, TidligereAvvist, DublettIKjoringen, IkkeLastet];
}

/// <summary>[Ny, issue #387] Årsaker til at et fasitutsagn ikke ble lastet, i tillegg til <see cref="Forslagsarsak"/>.</summary>
public static class Fasitarsak
{
    /// <summary>eId-en peker ikke på nøyaktig én gjeldende rettskilde i basen (ELI-en før «/nor/» i eId-en).</summary>
    public const string RettskildeIkkeFunnet = "rettskilde_ikke_funnet";
    /// <summary>eId-en finnes ikke som gjeldende node i rettskilden, eller sitatet står ikke i nodens tekst (eId-drift, #361 AK 7).</summary>
    public const string EidDrift = "eid_drift";
    /// <summary>Fasiten selv sier at utsagnet er omstridt eller uavklart — lastes ikke som faglig vurdert.</summary>
    public const string OmstridtIFasiten = "omstridt_i_fasiten";

    public static string Tekst(string arsak) => arsak switch
    {
        RettskildeIkkeFunnet => "eId-en peker ikke på nøyaktig én gjeldende rettskilde i basen",
        EidDrift => "eId-en finnes ikke som gjeldende node, eller sitatet står ikke i nodens tekst (eId-drift)",
        OmstridtIFasiten => "Fasiten markerer utsagnet som omstridt eller uavklart — lastes ikke som faglig vurdert",
        _ => Forslagsarsak.Tekst(arsak),
    };
}

/// <summary>
/// [Ny, issue #387] Et fasitutsagn med kilde UTENFOR korpus (FORMAT.md «utsagn_utenfor_korpus»): et utsagn som er avklart i
/// fasitkontrollen, men ikke står i lovteksten — første tilfelle er Domstoladministrasjonen <c>sekretariat_for</c> Tilsynsutvalget for
/// dommere (Ot.prp. nr. 44 (2000–2001) kap. 11.5.12, #311). Aktørene er id-er i samme fasitfil.
/// </summary>
public sealed record FasitUtsagnUtenforKorpus(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("kategori")] string Kategori,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("fra")] string Fra,
    [property: JsonPropertyName("til")] string? Til,
    [property: JsonPropertyName("objekt")] string? Objekt,
    [property: JsonPropertyName("polaritet")] string Polaritet,
    [property: JsonPropertyName("avgrensning")] string? Avgrensning,
    [property: JsonPropertyName("kilde")] FasitKildeUtenforKorpus Kilde,
    [property: JsonPropertyName("kunnskapsstatus")] string? Kunnskapsstatus,
    [property: JsonPropertyName("begrunnelse")] string? Begrunnelse,
    [property: JsonPropertyName("kommentar")] string? Kommentar,
    [property: JsonPropertyName("verifisert_av")] string? VerifisertAv);

/// <summary>[Ny, issue #387] Kilden utenfor korpus, med de feltene skriveveien krever (docs/33 §4.3).</summary>
public sealed record FasitKildeUtenforKorpus(
    [property: JsonPropertyName("tekst")] string Tekst,
    [property: JsonPropertyName("lenke")] string? Lenke,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("dokumentasjon")] string Dokumentasjon);

/// <summary>[Ny, issue #387] Én fasitfil lest fra disk.</summary>
public sealed record Fasitfil(string Navn, Strukturdokument Dokument, IReadOnlyList<FasitUtsagnUtenforKorpus> UtenforKorpus)
{
    /// <summary>De fem fasitfilene (data/fasit/strukturmodell/README.md). Andre JSON-filer i mappen (juristrunder, designtest) er ikke fasit.</summary>
    public static readonly string[] Filer = ["domstolloven", "energiloven", "helse-og-omsorgstjenesteloven", "sameloven", "spesialisthelsetjenesteloven"];

    public static Fasitfil Les(string mappe, string navn)
    {
        if (!Filer.Contains(navn))
        {
            throw new ArgumentException($"Ukjent fasitfil '{navn}'. Gyldige verdier: {string.Join(", ", Filer)}.");
        }
        var sti = Path.Combine(mappe, navn + ".json");
        if (!File.Exists(sti)) throw new ArgumentException($"Fant ikke fasitfilen '{sti}'.");
        var json = File.ReadAllText(sti);
        var dok = JsonSerializer.Deserialize<Strukturdokument>(json) ?? throw new ArgumentException($"Kunne ikke lese '{sti}'.");
        using var d = JsonDocument.Parse(json);
        var utenfor = d.RootElement.TryGetProperty("utsagn_utenfor_korpus", out var u)
            ? u.Deserialize<List<FasitUtsagnUtenforKorpus>>() ?? []
            : [];
        return new Fasitfil(navn, dok, utenfor);
    }
}

/// <summary>[Ny, issue #387] Én rad i fasitrapporten.</summary>
/// <param name="Premiss">Når en aktør er løst via en generisk binding eller standardregel: premisset og kunnskapsstatusen det har.</param>
/// <param name="Kunnskapsstatus">Kunnskapsstatusen raden fikk (etter svakeste premiss, docs/33 §4.6). Fasitens egen står i <paramref name="FasitKunnskapsstatus"/>.</param>
public sealed record FasitimportRad(
    string Fil, string UtsagnId, string? Eid, string? Sitat, string FasitKategori, string FasitType, string? Kategori, string? Typekode,
    string Utfall, string? Arsak, string? Detalj, Guid? KantId, string? FasitKunnskapsstatus, string? Kunnskapsstatus, string? Premiss)
{
    /// <summary>Når en aktør er løst på en annen måte enn eksakt navn: «entitetstype» (flertydig navn skilt på fasitens entitetstype) eller
    /// «referent» (fasitens referent slått opp eksakt).</summary>
    public string? Aktoroppslag { get; init; }
}

/// <summary>[Ny, issue #387] En aktør i fasiten som ikke kunne løses opp.</summary>
public sealed record FasitUloestAktor(string Fil, string AktorId, string Tekstform, string Status, string Arsak, int AntallUtsagn,
    IReadOnlyList<string> Treff, IReadOnlyList<string> Kandidater, string? FasitReferent, Guid? NavnekandidatId);

/// <summary>[Ny, issue #387] Tall per fil og utfall.</summary>
public sealed record FasitUtfalltall(string Fil, string Utfall, int Antall);

/// <summary>[Ny, issue #387] Rapporten fra én fasitimport (én eller flere filer).</summary>
public sealed record FasitimportRapport(
    bool Torrkjoring, int UtsagnTotalt, IReadOnlyList<FasitUtfalltall> PerUtfall, IReadOnlyList<ForslagArsaktall> IkkeLastetPerArsak,
    IReadOnlyList<FasitUloestAktor> UloesteAktorer, IReadOnlyList<FasitimportRad> Konflikter, IReadOnlyList<FasitimportRad> Rader)
{
    /// <summary>[ENDRET, Johanns avgjørelse 2026-10-26 på #387] Rader med <c>sikkerhet: lav</c> i fasiten. De lastes som
    /// <c>manuelt_foreslatt</c> med kunnskapsstatus <c>uavklart</c>, ikke som faglig vurdert, og vurderes i forslagskøen.</summary>
    public int LavSikkerhetSomForslag { get; init; }
    /// <summary>[Ny, Johanns avgjørelse 2026-10-26 på #387] Fasiten lagt til som bekreftelse (S1, <c>strukturkant_bekreftelser</c>) på kanter
    /// som alt var godkjent eller faglig vurdert med samme innhold — nye og som fantes fra en tidligere kjøring.</summary>
    public int BekreftelserNye { get; init; }
    public int BekreftelserFantes { get; init; }
    /// <summary>Rader der kunnskapsstatusen ble svekket fordi en aktør er løst via en binding/standardregel med svakere status.</summary>
    public int SvektEtterPremiss { get; init; }
    /// <summary>Aktører løst ved at et flertydig navn ble skilt på fasitens entitetstype, og ved fasitens referent (antall utsagn).</summary>
    public int LostViaEntitetstype { get; init; }
    public int LostViaReferent { get; init; }
    /// <summary>Navnetreffene (#385 AC3) når importen skrev.</summary>
    public Treffidentitet? Aktoridentitet { get; init; }
}

/// <summary>[Ny, issue #387, docs/33 §4.6, Johanns beslutning 2026-10-27] Rangeringen av kunnskapsstatus når premisser sammenlignes.</summary>
public static class Svakestepremiss
{
    /// <summary>Sterkest først: tekstfunn = eksternt dokumentert &gt; normalisering &gt; avledet &gt; fortolkning &gt; uavklart &gt; omstridt.
    /// Et premiss uten vurdert status (NULL) regnes ikke som sterkere enn uavklart. Foreldet bærer ingen slutning (regnes som omstridt).</summary>
    public static int Rang(string? kunnskapsstatus) => kunnskapsstatus switch
    {
        Strukturkanter.Tekstfunn or Strukturkanter.EksterntDokumentert => 0,
        Strukturkanter.Normalisering => 1,
        Strukturkanter.Avledet => 2,
        Strukturkanter.Fortolkning => 3,
        Strukturkanter.Uavklart or null => 4,
        _ => 5, // omstridt, foreldet
    };
}

public sealed partial class StrukturforslagTjeneste
{
    /// <summary>Prefikset på oppdagelseskilden for rader fra fasiten: <c>fasit:&lt;fil&gt;:&lt;id&gt;</c>.</summary>
    public const string FasitPrefiks = "fasit:";

    private sealed record Fasitpremiss(string Tekst, string? Kunnskapsstatus, string? Begrunnelse, IReadOnlyList<string> Argumenter, IReadOnlyList<Guid> KantIder);

    private sealed record Fasitoppslag(Kantnode? Node, string? Arsak, string Status, Fasitpremiss? Premiss, string? Via = null);

    /// <summary>[Ny, issue #387] Fasitens entitetstype → arten av node den kan være (FORMAT.md «entitetstype»). Null = ingen avgjørelse.</summary>
    private static string? ArtFor(string? entitetstype) => entitetstype switch
    {
        "rettssubjekt" or "organ" or "organisatorisk_enhet" or "person" => "virksomhet",
        "omrade" => "omrade",
        "klasse" => "klasse",
        "rolle" => "rolle",
        _ => null,
    };

    /// <summary>
    /// [Ny, issue #387] Laster én fasitfil som <c>faglig_vurdert</c>. Per utsagn:
    /// <list type="number">
    /// <item>rettskilden fra eId-en (ELI-en før «/nor/», gjeldende versjon), og eId-en og sitatet kontrolleres mot gjeldende node (drift listes);</item>
    /// <item>aktørene løses opp med samme klassifisering som forslagskøen (eksakt navn/navneform/begrep, eller generisk med vurdert binding eller
    /// standardregel). Ikke løst = ikke lastet, listet — ingenting gjettes og ingen aktør opprettes;</item>
    /// <item>kunnskapsstatus er fasitens; er en aktør løst via en binding/standardregel med svakere status, svekkes den til det svakeste premisset
    /// (docs/33 §4.6);</item>
    /// <item>finnes raden fra før med samme opphav: avvist → respekteres; lik → uendret; endret → revisjon (bare når den fortsatt er faglig vurdert,
    /// ellers konflikt);</item>
    /// <item>finnes samme utsagn fra en annen kilde: avvist → respekteres; godkjent/faglig vurdert → uendret eller KONFLIKT (aldri overskrevet);
    /// ventende forslag → revidert til fasitraden (forslaget blir foreldet).</item>
    /// </list>
    /// <paramref name="torrkjoring"/> skriver ingenting (ingen kanter, navneformer eller navnekandidater). <paramref name="navnekandidater"/> legger
    /// aktører som ikke kunne løses opp i navnekandidatkøen (som forslagskøen gjør).
    /// </summary>
    public async Task<FasitimportRapport> ImporterFasitAsync(
        Fasitfil fil, bool torrkjoring, bool navnekandidater, string bruker, CancellationToken ct = default)
    {
        var dok = fil.Dokument with { Utsagn = fil.Dokument.Utsagn.Select(Strukturkontrakt.NormaliserOmradesammensetning).ToList() };
        var aktorer = dok.Aktorer.ToDictionary(a => a.Id, StringComparer.Ordinal);
        var rader = new List<FasitimportRad>();
        var uloest = new Dictionary<string, (string Arsak, string Status, int Antall, Guid RettskildeId, string Eid, IReadOnlyList<string> Treff, IReadOnlyList<string> Kandidater)>(StringComparer.Ordinal);
        var settIKjoringen = new HashSet<(Nokkel, string?)>();
        var lavSikkerhet = 0;
        var bekreftelser = new Bekreftelsestall();
        var svekket = 0;
        var viaEntitetstype = 0;
        var viaReferent = 0;
        var klassifiserere = new Dictionary<Guid, Aktorklassifiserer>();
        var rettskildeForEli = new Dictionary<string, Guid?>(StringComparer.Ordinal);
        var eidsPerRettskilde = new Dictionary<Guid, Dictionary<string, HashSet<string>>>();

        async Task<Aktorklassifiserer> KlassifisererAsync(Guid rid) =>
            klassifiserere.TryGetValue(rid, out var k) ? k : klassifiserere[rid] = await NyKlassifisererAsync(rid, ct);

        async Task<Guid?> RettskildeAsync(string eliEllerEid)
        {
            var i = eliEllerEid.IndexOf("/nor", StringComparison.Ordinal);
            if (i < 0) return null;
            var eli = eliEllerEid[..(i + 4)];
            if (rettskildeForEli.TryGetValue(eli, out var r)) return r;
            var treff = await db.Rettskilder.Where(x => x.Eli == eli && x.Entitetsstatus == "gjeldende").Select(x => x.Id).Take(2).ToListAsync(ct);
            return rettskildeForEli[eli] = treff.Count == 1 ? treff[0] : null;
        }

        // En eId uten ELI («kap-I/ledd-1» i et delegeringsvedtak): rettskilden er den ene av fasitens kilder (hoved + ledsagende) som har en
        // gjeldende node med eId-en. Ingen eller flere = ikke funnet (ingen gjetting).
        var kildeElis = new[] { dok.Eli }.Concat(dok.Ledsagende.Select(l => l.Eli)).Distinct(StringComparer.Ordinal).ToList();
        async Task<Guid?> RettskildeForEidAsync(string eid)
        {
            if (eid.Contains("/nor", StringComparison.Ordinal)) return await RettskildeAsync(eid);
            var treff = new List<Guid>();
            foreach (var eli in kildeElis)
            {
                if (await RettskildeAsync(eli) is { } r
                    && await db.RettskildeNoder.AnyAsync(n => n.RettskildeId == r && n.Eid == eid && n.Entitetsstatus == "gjeldende", ct)) treff.Add(r);
            }
            return treff.Count == 1 ? treff[0] : null;
        }

        async Task<Fasitoppslag> SlaOppAsync(string aktorId, Guid rid, string eid)
        {
            var kl = await KlassifisererAsync(rid);
            var aktor = aktorer[aktorId];
            var g = await kl.GrunnlagAsync(aktor, ct);
            var (status, node, _, _) = await kl.ForekomstAsync(g, eid, ct);
            // [#387] Et flertydig navn (Karasjok = kommunen OG området) skilles på fasitens entitetstype når nøyaktig ett treff har den arten.
            if (status == Aktorstatus.Flertydig && g.Status == Aktorstatus.Flertydig && ArtFor(aktor.Entitetstype) is { } art)
            {
                var avType = g.Treffnoder.Where(t => t.Art == art).Select(t => t.Node).Distinct().ToList();
                if (avType.Count == 1) return new Fasitoppslag(avType[0], null, Aktorstatus.Kjent, null, "entitetstype");
            }
            // [#387] Fasitens referent (bare satt når teksten ALENE avgjør den, FORMAT.md) slås opp eksakt som navn eller navneform.
            if (status is Aktorstatus.NyAktor or Aktorstatus.NyNavneformForKjentAktor && !string.IsNullOrWhiteSpace(aktor.Referent)
                && await virksomhetOppslag.FinnVirksomhetIdForNavnEllerNavneformAsync(aktor.Referent, ct) is { } refId)
            {
                return new Fasitoppslag(Kantnode.Virksomhet(refId), null, Aktorstatus.Kjent, null, "referent");
            }
            if (status != Aktorstatus.Kjent)
            {
                var arsak = status switch
                {
                    Aktorstatus.Flertydig => Forslagsarsak.AktorFlertydig,
                    Aktorstatus.Generisk => Forslagsarsak.AktorGeneriskUtenBinding,
                    _ => Forslagsarsak.AktorIkkeIKatalogen,
                };
                return new Fasitoppslag(null, arsak, status, null);
            }
            // Generisk omtale løst via vurdert binding eller standardregel: det er et PREMISS (docs/33 §4.6).
            Fasitpremiss? premiss = null;
            if (g.GeneriskNavneformId is { } nid && g.Status == Aktorstatus.Generisk)
            {
                var o = await generiske.LosOppAsync(nid, rid, eid, ct);
                var navn = aktorer[aktorId].Tekstform;
                if (o.Status == GeneriskOppslagstatus.Bundet && o.Binding is { } b)
                {
                    premiss = new Fasitpremiss($"«{navn}» er løst via bindingen {b.Id} ({b.Status})", b.Kunnskapsstatus, b.Begrunnelse,
                        b.Tolkningsargumenter ?? [], []);
                }
                else if (o.Status == GeneriskOppslagstatus.Regel && o.Regel is { } r)
                {
                    // Regelens svar er avledet; premissene dens (ressortfordelingen, klassen …) kan være svakere.
                    var svakest = new[] { r.Kunnskapsstatus }.Concat(r.Premisser.Select(p => p.Kunnskapsstatus))
                        .OrderByDescending(Svakestepremiss.Rang).First();
                    premiss = new Fasitpremiss($"«{navn}» er løst via standardregelen {r.Regelkode} ({r.Forklaring})", svakest, r.Forklaring, [],
                        r.Premisser.Where(p => p.KantId is not null).Select(p => p.KantId!.Value).ToList());
                }
            }
            return new Fasitoppslag(node, null, status, premiss);
        }

        void Uloest(string aktorId, Fasitoppslag o, Guid rid, string eid, Aktorgrunnlag g) =>
            uloest[aktorId] = uloest.TryGetValue(aktorId, out var f) ? f with { Antall = f.Antall + 1 } : (o.Arsak!, o.Status, 1, rid, eid, g.Treff, g.Kandidater);

        // ---- Utsagn i korpus ----
        foreach (var u in dok.Utsagn)
        {
            var kilde = $"{FasitPrefiks}{fil.Navn}:{u.Id}";
            FasitimportRad Rad(string? kat, string? kode, string utfall, string? arsak, string? detalj, Guid? kantId, string? ks = null, string? premiss = null) =>
                new(fil.Navn, u.Id, u.Eid, u.Sitat, u.Kategori, u.Type, kat, kode, utfall, arsak, detalj, kantId, u.Kunnskapsstatus, ks, premiss);

            if (u.Kunnskapsstatus is Strukturkanter.Omstridt or Strukturkanter.Uavklart)
            {
                rader.Add(Rad(null, null, Fasitutfall.IkkeLastet, Fasitarsak.OmstridtIFasiten, u.Kunnskapsstatus, null));
                continue;
            }
            if (await RettskildeForEidAsync(u.Eid) is not { } rid)
            {
                rader.Add(Rad(null, null, Fasitutfall.IkkeLastet, Fasitarsak.RettskildeIkkeFunnet, u.Eid, null));
                continue;
            }
            // eId-drift (#361 AK 7, #405): noden må finnes i gjeldende versjon, og sitatet må stå i den. Ingen ny avbildning gjettes.
            var tekst = await db.RettskildeNoder.Where(n => n.RettskildeId == rid && n.Eid == u.Eid && n.Entitetsstatus == "gjeldende")
                .Select(n => n.Tekst).FirstOrDefaultAsync(ct);
            if (tekst is null || !tekst.Contains(u.Sitat, StringComparison.Ordinal))
            {
                rader.Add(Rad(null, null, Fasitutfall.IkkeLastet, Fasitarsak.EidDrift,
                    tekst is null ? "eId-en finnes ikke som gjeldende node" : "sitatet står ikke i nodens tekst", null));
                continue;
            }
            if (KanttypeFor(u.Kategori, u.Type) is not { } kanttype)
            {
                rader.Add(Rad(null, null, Fasitutfall.IkkeLastet, Forslagsarsak.TypeUtenTypekode, $"{u.Kategori}/{u.Type}", null));
                continue;
            }
            var (kat, kode) = kanttype;
            if (u.Fra is null)
            {
                rader.Add(Rad(kat, kode, Fasitutfall.IkkeLastet, Forslagsarsak.FraIkkeITeksten, null, null));
                continue;
            }
            var fra = await SlaOppAsync(u.Fra, rid, u.Eid);
            var til = u.Til is null ? null : await SlaOppAsync(u.Til, rid, u.Eid);
            var mislyktes = new[] { (Id: u.Fra, O: fra), (Id: u.Til, O: til) }.Where(x => x.O is { Node: null }).ToList();
            if (mislyktes.Count > 0)
            {
                foreach (var (id, o) in mislyktes)
                {
                    Uloest(id!, o!, rid, u.Eid, await (await KlassifisererAsync(rid)).GrunnlagAsync(aktorer[id!], ct));
                }
                rader.Add(Rad(kat, kode, Fasitutfall.IkkeLastet, mislyktes[0].O!.Arsak,
                    string.Join("; ", mislyktes.Select(m => $"«{aktorer[m.Id!].Tekstform}»")), null));
                continue;
            }
            foreach (var id in new[] { u.Fra, u.Til }.OfType<string>())
            {
                if (!eidsPerRettskilde.TryGetValue(rid, out var e)) eidsPerRettskilde[rid] = e = new(StringComparer.Ordinal);
                if (!e.TryGetValue(id, out var s)) e[id] = s = new(StringComparer.Ordinal);
                s.Add(u.Eid);
            }

            var merknader = Merknader(u.Sikkerhet, u.KildeUtenforKorpus, u.VerifisertAv);
            var (ks, begrunnelse, argumenter, premisser, premissTekst) = SvakestePremiss(
                u.Kunnskapsstatus, u.Begrunnelse ?? (u.Kunnskapsstatus == Strukturkanter.Normalisering ? "Regel: fasiten (#367)" : null),
                u.Tolkningsargumenter, [fra.Premiss, til?.Premiss]);
            if (ks != u.Kunnskapsstatus) svekket++;
            // [Ny, Johanns avgjørelse 2026-10-26 på #387] Lav sikkerhet i fasiten = annotatøren var usikker. Raden er da ikke faglig vurdert:
            // den blir et manuelt forslag med kunnskapsstatus uavklart (eller omstridt, om et premiss er det), og vurderes i forslagskøen.
            var lav = u.Sikkerhet == "lav";
            if (lav && Svakestepremiss.Rang(ks) < Svakestepremiss.Rang(Strukturkanter.Uavklart))
            {
                begrunnelse = $"Fasiten markerer sikkerhet lav (annotatøren var usikker){(ks is null ? "" : $"; fasitens kunnskapsstatus var {ks}")}"
                    + $"{(begrunnelse is null ? "" : $" ({begrunnelse})")}. Johanns avgjørelse 2026-10-26 på #387: manuelt forslag, vurderes i forslagskøen.";
                ks = Strukturkanter.Uavklart;
                argumenter = null;
                premisser = null;
            }
            var ny = new NyStrukturkant(
                kat, kode, fra.Node!, til?.Node,
                HjemmelRettskildeId: rid, HjemmelEid: u.Eid,
                AvgrensningTekst: u.Avgrensning, Objekt: u.Objekt, Polaritet: u.Polaritet,
                Kommentar: $"«{u.Sitat}»" + (string.IsNullOrWhiteSpace(u.Kommentar) ? "" : $" — {u.Kommentar}") + merknader,
                Status: lav ? Strukturkanter.ManueltForeslatt : Strukturkanter.FagligVurdert, OppdagelsesKilde: kilde,
                Normform: u.Normform, Grunnlag: u.Grunnlag, Delegeringsadgang: u.Delegeringsadgang, DelegeringSlutning: u.DelegeringSlutning,
                Undertype: u.Undertype, Modalitet: u.Modalitet,
                Kunnskapsstatus: ks, Begrunnelse: begrunnelse, Tolkningsargumenter: argumenter, Premisser: premisser);
            var (utfall, detalj, kantId) = await SkrivFasitradAsync(ny, kilde, rid, settIKjoringen, torrkjoring, bruker, bekreftelser, ct);
            if (utfall is not Fasitutfall.IkkeLastet && lav) lavSikkerhet++;
            var via = new[] { fra.Via, til?.Via }.OfType<string>().Distinct().ToList();
            if (via.Contains("entitetstype")) viaEntitetstype++;
            if (via.Contains("referent")) viaReferent++;
            rader.Add(Rad(kat, kode, utfall, utfall == Fasitutfall.IkkeLastet ? Forslagsarsak.AvvistAvValideringen : null, detalj, kantId, ks, premissTekst)
                with { Aktoroppslag = via.Count == 0 ? null : string.Join(", ", via) });
        }

        // ---- Utsagn med kilde utenfor korpus (FORMAT.md «utsagn_utenfor_korpus») ----
        var hovedkilde = await RettskildeAsync(dok.Eli);
        foreach (var u in fil.UtenforKorpus)
        {
            var kilde = $"{FasitPrefiks}{fil.Navn}:{u.Id}";
            FasitimportRad Rad(string? kat, string? kode, string utfall, string? arsak, string? detalj, Guid? kantId, string? ks = null) =>
                new(fil.Navn, u.Id, null, null, u.Kategori, u.Type, kat, kode, utfall, arsak, detalj, kantId, u.Kunnskapsstatus, ks, null);
            if (hovedkilde is not { } rid)
            {
                rader.Add(Rad(null, null, Fasitutfall.IkkeLastet, Fasitarsak.RettskildeIkkeFunnet, dok.Eli, null));
                continue;
            }
            if (KanttypeFor(u.Kategori, u.Type) is not { } kanttype)
            {
                rader.Add(Rad(null, null, Fasitutfall.IkkeLastet, Forslagsarsak.TypeUtenTypekode, $"{u.Kategori}/{u.Type}", null));
                continue;
            }
            var (kat, kode) = kanttype;
            // Aktøroppslaget i hovedrettskildens kontekst; uten eId gjelder bare eksakte treff (en generisk omtale har ingen node å bindes i).
            var fra = await SlaOppAsync(u.Fra, rid, "");
            var til = u.Til is null ? null : await SlaOppAsync(u.Til, rid, "");
            var mislyktes = new[] { (Id: u.Fra, O: fra), (Id: u.Til, O: til) }.Where(x => x.O is { Node: null }).ToList();
            if (mislyktes.Count > 0 || fra.Premiss is not null || til?.Premiss is not null)
            {
                foreach (var (id, o) in mislyktes) Uloest(id!, o!, rid, "", await (await KlassifisererAsync(rid)).GrunnlagAsync(aktorer[id!], ct));
                rader.Add(Rad(kat, kode, Fasitutfall.IkkeLastet, mislyktes.Count > 0 ? mislyktes[0].O!.Arsak : Forslagsarsak.AktorGeneriskUtenBinding,
                    string.Join("; ", new[] { u.Fra, u.Til }.OfType<string>().Select(id => $"«{aktorer[id].Tekstform}»")), null));
                continue;
            }
            var ny = new NyStrukturkant(
                kat, kode, fra.Node!, til?.Node,
                KildeUtenforKorpusTekst: u.Kilde.Tekst, KildeUtenforKorpusLenke: u.Kilde.Lenke,
                KildeUtenforKorpusType: u.Kilde.Type, KildeUtenforKorpusDokumentasjon: u.Kilde.Dokumentasjon,
                AvgrensningTekst: u.Avgrensning, Objekt: u.Objekt, Polaritet: u.Polaritet,
                Kommentar: (u.Kommentar ?? "").Trim() + Merknader(null, null, u.VerifisertAv),
                Status: Strukturkanter.FagligVurdert, OppdagelsesKilde: kilde,
                Kunnskapsstatus: u.Kunnskapsstatus, Begrunnelse: u.Begrunnelse);
            var (utfall, detalj, kantId) = await SkrivFasitradAsync(ny, kilde, null, settIKjoringen, torrkjoring, bruker, bekreftelser, ct);
            rader.Add(Rad(kat, kode, utfall, utfall == Fasitutfall.IkkeLastet ? Forslagsarsak.AvvistAvValideringen : null, detalj, kantId, u.Kunnskapsstatus));
        }

        // ---- Aktøridentitet (#385 AC3) og navnekandidater — bare når importen skriver ----
        Treffidentitet? identitet = null;
        var uloesteAktorer = new List<FasitUloestAktor>();
        if (!torrkjoring)
        {
            identitet = new Treffidentitet();
            foreach (var (rid, eids) in eidsPerRettskilde)
            {
                var kl = await KlassifisererAsync(rid);
                var del = dok with { Aktorer = dok.Aktorer.Where(a => eids.ContainsKey(a.Id)).ToList(), Utsagn = dok.Utsagn.Where(u => (u.Fra is not null && eids.TryGetValue(u.Fra, out var s1) && s1.Contains(u.Eid)) || (u.Til is not null && eids.TryGetValue(u.Til, out var s2) && s2.Contains(u.Eid))).ToList() };
                identitet += await SikreAktoridentitetAsync(rid, del, kl, $"fasit:{fil.Navn}", bruker, ct);
            }
        }
        foreach (var (aktorId, (arsak, status, antall, rid, eid, treff, kandidater)) in uloest)
        {
            var a = aktorer[aktorId];
            Guid? kandidatId = null;
            if (!torrkjoring && navnekandidater && eid != "" && arsak is Forslagsarsak.AktorIkkeIKatalogen or Forslagsarsak.AktorGeneriskUtenBinding)
            {
                kandidatId = await OpprettNavnekandidatAsync(rid, eid, a.Tekstform, bruker, ct);
            }
            uloesteAktorer.Add(new FasitUloestAktor(fil.Navn, aktorId, a.Tekstform, status, arsak, antall, treff, kandidater.Take(5).ToList(), a.Referent, kandidatId));
        }

        return Rapport(torrkjoring, rader, uloesteAktorer) with
        {
            LavSikkerhetSomForslag = lavSikkerhet, BekreftelserNye = bekreftelser.Nye, BekreftelserFantes = bekreftelser.Fantes,
            SvektEtterPremiss = svekket, LostViaEntitetstype = viaEntitetstype, LostViaReferent = viaReferent,
            Aktoridentitet = identitet,
        };
    }

    /// <summary>[Ny, issue #387] Slår sammen rapportene fra flere filer.</summary>
    public static FasitimportRapport SlaSammen(bool torrkjoring, IReadOnlyList<FasitimportRapport> rapporter)
    {
        var r = Rapport(torrkjoring, rapporter.SelectMany(x => x.Rader).ToList(), rapporter.SelectMany(x => x.UloesteAktorer).ToList());
        var identitet = rapporter.Select(x => x.Aktoridentitet).OfType<Treffidentitet>().ToList();
        return r with
        {
            LavSikkerhetSomForslag = rapporter.Sum(x => x.LavSikkerhetSomForslag),
            BekreftelserNye = rapporter.Sum(x => x.BekreftelserNye),
            BekreftelserFantes = rapporter.Sum(x => x.BekreftelserFantes),
            SvektEtterPremiss = rapporter.Sum(x => x.SvektEtterPremiss),
            LostViaEntitetstype = rapporter.Sum(x => x.LostViaEntitetstype),
            LostViaReferent = rapporter.Sum(x => x.LostViaReferent),
            Aktoridentitet = identitet.Count == 0 ? null : identitet.Aggregate(new Treffidentitet(), (a, b) => a + b),
        };
    }

    private static FasitimportRapport Rapport(bool torrkjoring, List<FasitimportRad> rader, List<FasitUloestAktor> uloeste)
    {
        var perUtfall = rader.GroupBy(r => (r.Fil, r.Utfall))
            .Select(g => new FasitUtfalltall(g.Key.Fil, g.Key.Utfall, g.Count()))
            .OrderBy(t => t.Fil, StringComparer.Ordinal).ThenBy(t => Array.IndexOf(Fasitutfall.Alle, t.Utfall)).ToList();
        var perArsak = rader.Where(r => r.Arsak is not null).GroupBy(r => r.Arsak!)
            .Select(g => new ForslagArsaktall(g.Key, Fasitarsak.Tekst(g.Key), g.Count(),
                g.Take(5).Select(x => new ForslagsutfallRad($"{x.Fil}:{x.UtsagnId}", x.Eid ?? "", x.Sitat ?? "", x.FasitKategori, x.FasitType,
                    x.Kategori, x.Typekode, x.Utfall, x.Arsak, x.Detalj, x.KantId, $"{FasitPrefiks}{x.Fil}:{x.UtsagnId}")).ToList()))
            .OrderByDescending(a => a.Antall).ToList();
        return new FasitimportRapport(torrkjoring, rader.Count, perUtfall, perArsak,
            uloeste.OrderByDescending(a => a.AntallUtsagn).ThenBy(a => a.Fil, StringComparer.Ordinal).ThenBy(a => a.Tekstform, StringComparer.Ordinal).ToList(),
            rader.Where(r => r.Utfall == Fasitutfall.Konflikt).ToList(), rader);
    }

    private static string Merknader(string? sikkerhet, bool? kildeUtenforKorpus, string? verifisertAv)
    {
        var m = new List<string>();
        if (sikkerhet == "lav") m.Add("fasitens sikkerhet: lav");
        if (kildeUtenforKorpus == true) m.Add("fasiten: utsagnet viser til noe som fastsettes utenfor teksten (kilde_utenfor_korpus)");
        if (!string.IsNullOrWhiteSpace(verifisertAv)) m.Add($"verifisert av {verifisertAv}");
        return m.Count == 0 ? "" : $" [{string.Join("; ", m)}]";
    }

    /// <summary>
    /// [Ny, issue #387, docs/33 §4.6] Fasitens kunnskapsstatus står, med mindre en aktør er løst via et premiss med svakere status. Da blir
    /// raden ikke sterkere enn det svakeste premisset: fortolkning (med premissets argumenter), avledet (med premissets kanter), ellers uavklart
    /// eller omstridt. Fasiten uten vurdert status (NULL) er ikke sterkere enn noe og står.
    /// </summary>
    private static (string? Ks, string? Begrunnelse, IReadOnlyList<string>? Argumenter, IReadOnlyList<Guid>? Premisser, string? PremissTekst) SvakestePremiss(
        string? fasitKs, string? fasitBegrunnelse, IReadOnlyList<string>? fasitArgumenter, IEnumerable<Fasitpremiss?> premisser)
    {
        var liste = premisser.OfType<Fasitpremiss>().ToList();
        var premissTekst = liste.Count == 0 ? null
            : string.Join("; ", liste.Select(p => $"{p.Tekst}, kunnskapsstatus {p.Kunnskapsstatus ?? "ikke vurdert"}"));
        if (fasitKs is null || liste.Count == 0) return (fasitKs, fasitBegrunnelse, fasitArgumenter, null, premissTekst);
        var svakest = liste.OrderByDescending(p => Svakestepremiss.Rang(p.Kunnskapsstatus)).First();
        if (Svakestepremiss.Rang(svakest.Kunnskapsstatus) <= Svakestepremiss.Rang(fasitKs)) return (fasitKs, fasitBegrunnelse, fasitArgumenter, null, premissTekst);
        var grunn = $"Fasiten: {fasitKs}{(fasitBegrunnelse is null ? "" : $" ({fasitBegrunnelse})")}. {svakest.Tekst} med kunnskapsstatus "
            + $"{svakest.Kunnskapsstatus ?? "ikke vurdert"}{(svakest.Begrunnelse is null ? "" : $" ({svakest.Begrunnelse})")}. Utsagnet er ikke sterkere enn det "
            + "svakeste premisset (docs/33 §4.6).";
        var argumenter = (fasitArgumenter ?? []).Concat(svakest.Argumenter).Distinct().ToList();
        return svakest.Kunnskapsstatus switch
        {
            Strukturkanter.Fortolkning when argumenter.Count > 0 => (Strukturkanter.Fortolkning, grunn, argumenter, null, premissTekst),
            Strukturkanter.Avledet when svakest.KantIder.Count > 0 => (Strukturkanter.Avledet, grunn, null, svakest.KantIder, premissTekst),
            Strukturkanter.Omstridt or Strukturkanter.Foreldet => (Strukturkanter.Omstridt, grunn, null, null, premissTekst),
            _ => (Strukturkanter.Uavklart, grunn, null, null, premissTekst),
        };
    }

    /// <summary>Innholdet som sammenlignes når samme utsagn finnes fra før (utover nøkkelen).</summary>
    private static List<string> Forskjeller(StrukturkantEntitet k, NyStrukturkant ny, bool helt)
    {
        static string? N(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        var f = new List<string>();
        void Sjekk(string felt, string? a, string? b) { if (N(a) != N(b)) f.Add($"{felt}: «{N(a) ?? "—"}» i basen, «{N(b) ?? "—"}» i fasiten"); }
        Sjekk("objekt", k.Objekt, ny.Objekt);
        Sjekk("avgrensning", k.AvgrensningTekst, ny.AvgrensningTekst);
        Sjekk("kunnskapsstatus", k.Kunnskapsstatus, ny.Kunnskapsstatus);
        Sjekk("delegeringsadgang", k.Delegeringsadgang, ny.Delegeringsadgang);
        Sjekk("delegering_slutning", k.DelegeringSlutning, ny.DelegeringSlutning);
        Sjekk("grunnlag", k.Grunnlag, ny.Grunnlag);
        if (helt)
        {
            Sjekk("begrunnelse", k.Begrunnelse, ny.Begrunnelse);
            Sjekk("tolkningsargumenter", string.Join(",", k.Tolkningsargumenter ?? []), string.Join(",", ny.Tolkningsargumenter ?? []));
            Sjekk("kommentar", k.Kommentar, ny.Kommentar);
            Sjekk("kategori/type", $"{k.Kategori}/{k.Typekode}", $"{ny.Kategori}/{ny.Typekode}");
            Sjekk("godkjenning", k.Status, ny.Status); // [Ny, #387-oppfølgingen] lav sikkerhet: faglig vurdert → manuelt forslag
            Sjekk("fra", $"{k.FraVirksomhetId}{k.FraBegrepId}", $"{ny.Fra.VirksomhetId}{ny.Fra.BegrepId}");
            Sjekk("til", $"{k.TilVirksomhetId}{k.TilBegrepId}", $"{ny.Til?.VirksomhetId}{ny.Til?.BegrepId}");
            Sjekk("hjemmel", $"{k.HjemmelRettskildeId} {k.HjemmelEid}", $"{ny.HjemmelRettskildeId} {ny.HjemmelEid}");
            Sjekk("kilde utenfor korpus", k.KildeUtenforKorpusTekst, ny.KildeUtenforKorpusTekst);
            Sjekk("polaritet/undertype/normform/modalitet", $"{k.Polaritet}/{k.Undertype}/{k.Normform}/{k.Modalitet}", $"{ny.Polaritet}/{ny.Undertype}/{ny.Normform}/{ny.Modalitet}");
        }
        return f;
    }

    /// <summary>[Ny, issue #387] Skriver (eller tørrkjører) én fasitrad etter reglene i <see cref="ImporterFasitAsync"/>.</summary>
    private sealed class Bekreftelsestall { public int Nye; public int Fantes; }

    /// <summary>
    /// [Ny, Johanns avgjørelse 2026-10-26 på #387] Fasiten som BEKREFTELSE på en kant som alt var godkjent eller faglig vurdert med samme innhold:
    /// samme mekanisme som S1 i #408 (<see cref="StrukturkantBekreftelseEntitet"/>). Bare et ekstra grunnlag — kantens status, hjemmel og
    /// opphav endres ikke. Opphavet står i bekreftelsens oppdagelseskilde (<c>fasit:&lt;fil&gt;:&lt;id&gt;</c>). Bare for rader med hjemmel i korpus
    /// (tabellen krever det). Idempotent på (kant, rettskilde, eId).
    /// </summary>
    private async Task BekreftAsync(Guid kantId, NyStrukturkant ny, string kilde, bool torrkjoring, string bruker, Bekreftelsestall tall, CancellationToken ct)
    {
        if (ny.HjemmelRettskildeId is not { } rid || ny.HjemmelEid is not { } eid) return;
        if (await db.StrukturkantBekreftelser.AnyAsync(b => b.KantId == kantId && b.HjemmelRettskildeId == rid && b.HjemmelEid == eid, ct))
        {
            tall.Fantes++;
            return;
        }
        tall.Nye++;
        if (torrkjoring) return;
        db.StrukturkantBekreftelser.Add(new StrukturkantBekreftelseEntitet
        {
            Id = Guid.NewGuid(), KantId = kantId, HjemmelRettskildeId = rid, HjemmelEid = eid, OppdagelsesKilde = kilde,
            Kommentar = "Fasiten (faglig vurdert, ikke uavhengig kontrollert): " + ny.Kommentar, OpprettetAv = bruker,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task<(string Utfall, string? Detalj, Guid? KantId)> SkrivFasitradAsync(
        NyStrukturkant ny, string kilde, Guid? rettskildeId, HashSet<(Nokkel, string?)> settIKjoringen, bool torrkjoring, string bruker,
        Bekreftelsestall bekreftelser, CancellationToken ct)
    {
        var nokkel = new Nokkel(ny.HjemmelEid ?? "", ny.Kategori, ny.Typekode, ny.Fra.VirksomhetId, ny.Fra.BegrepId, ny.Til?.VirksomhetId, ny.Til?.BegrepId,
            string.IsNullOrWhiteSpace(ny.Undertype) ? null : ny.Undertype.Trim(), string.IsNullOrWhiteSpace(ny.Normform) ? null : ny.Normform.Trim(),
            ny.Polaritet, string.IsNullOrWhiteSpace(ny.Modalitet) ? null : ny.Modalitet.Trim());
        var objekt = string.IsNullOrWhiteSpace(ny.Objekt) ? null : ny.Objekt.Trim();
        if (!settIKjoringen.Add((nokkel, objekt))) return (Fasitutfall.DublettIKjoringen, null, null);

        async Task<(string, string?, Guid?)> ReviderAsync(StrukturkantEntitet gammel, string utfall)
        {
            if (torrkjoring) return (utfall, null, gammel.Id);
            try
            {
                var nyKant = await kanter.ReviderAsync(gammel.Id, ny, bruker, ct);
                return (utfall, null, nyKant!.Id);
            }
            catch (ArgumentException ex) { return (Fasitutfall.IkkeLastet, ex.Message, null); }
        }

        async Task<(string, string?, Guid?)> BekreftOgSvarAsync(StrukturkantEntitet k)
        {
            await BekreftAsync(k.Id, ny, kilde, torrkjoring, bruker, bekreftelser, ct);
            return (Fasitutfall.FantesVurdert, $"{k.Status}, {k.OppdagelsesKilde}", k.Id);
        }

        // 1. Samme opphav fra en tidligere import.
        var sammeOpphav = await kanter.GjeldendeInklAvviste.Where(k => k.OppdagelsesKilde == kilde).OrderBy(k => k.OpprettetTidspunkt).FirstOrDefaultAsync(ct);
        if (sammeOpphav is not null)
        {
            if (sammeOpphav.Status == Strukturkanter.Avvist) return (Fasitutfall.TidligereAvvist, sammeOpphav.AvvisningBegrunnelse, sammeOpphav.Id);
            var f = Forskjeller(sammeOpphav, ny, helt: true);
            if (f.Count == 0) return (Fasitutfall.Uendret, null, sammeOpphav.Id);
            if (sammeOpphav.Status == Strukturkanter.Godkjent) return (Fasitutfall.Konflikt, "Godkjent etter forrige import; fasiten er endret: " + string.Join("; ", f), sammeOpphav.Id);
            return await ReviderAsync(sammeOpphav, Fasitutfall.Oppdatert);
        }

        // 2. Samme utsagn fra en annen kilde (nøkkelen fra forslagskøen + objektet).
        var kandidater = await kanter.GjeldendeInklAvviste.Where(k =>
                k.HjemmelRettskildeId == rettskildeId && k.HjemmelEid == ny.HjemmelEid
                && k.Kategori == nokkel.Kategori && k.Typekode == nokkel.Typekode
                && k.FraVirksomhetId == nokkel.FraV && k.FraBegrepId == nokkel.FraB && k.TilVirksomhetId == nokkel.TilV && k.TilBegrepId == nokkel.TilB
                && k.Undertype == nokkel.Undertype && k.Normform == nokkel.Normform && k.Polaritet == nokkel.Polaritet && k.Modalitet == nokkel.Modalitet
                && (rettskildeId != null || k.KildeUtenforKorpusTekst == ny.KildeUtenforKorpusTekst))
            .ToListAsync(ct);
        // Samme objekt først; ellers en rad med tomt objekt (konverteringen setter det sjelden) — aldri en rad med et ANNET objekt.
        var fantes = kandidater.FirstOrDefault(k => k.Objekt == objekt) ?? kandidater.FirstOrDefault(k => k.Objekt is null);
        if (fantes is not null)
        {
            if (fantes.Status == Strukturkanter.Avvist) return (Fasitutfall.TidligereAvvist, fantes.AvvisningBegrunnelse, fantes.Id);
            if (fantes.Status is Strukturkanter.Godkjent or Strukturkanter.FagligVurdert)
            {
                var f = Forskjeller(fantes, ny, helt: false);
                if (f.Count > 0) return (Fasitutfall.Konflikt, $"{fantes.Status}, {fantes.OppdagelsesKilde}: " + string.Join("; ", f), fantes.Id);
                return await BekreftOgSvarAsync(fantes);
            }
            return await ReviderAsync(fantes, Fasitutfall.ErstattetForslag);
        }

        // 3. Nytt utsagn.
        if (torrkjoring) return (Fasitutfall.Ny, null, null);
        try
        {
            var resultat = await kanter.OpprettAsync(ny, bruker, ct);
            if (resultat.VarNy) return (Fasitutfall.Ny, null, resultat.Kant.Id);
            // Skriveveiens strengere identitet traff en rad nøkkelen over ikke fant (annet paragrafspenn e.l.) — samme regler.
            var k = resultat.Kant;
            return k.Status == Strukturkanter.Avvist ? (Fasitutfall.TidligereAvvist, null, k.Id)
                : k.Status is Strukturkanter.Godkjent or Strukturkanter.FagligVurdert
                    ? (Forskjeller(k, ny, false) is { Count: > 0 } f ? (Fasitutfall.Konflikt, string.Join("; ", f), k.Id) : await BekreftOgSvarAsync(k))
                    : await ReviderAsync(k, Fasitutfall.ErstattetForslag);
        }
        catch (ArgumentException ex) { return (Fasitutfall.IkkeLastet, ex.Message, null); }
    }
}
