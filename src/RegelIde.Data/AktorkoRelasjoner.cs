using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

// [Ny, issue #469, Johanns klarsignal 2026-11-17] Forhåndsklassifiseringens relasjoner (klassifisering.json, PR #462) skrives som STRUKTURKANTER,
// én knapp per relasjon i aktørkøen. Avgjort i #469: «relasjonene skrives som strukturkanter (G, M, I, A) med hjemmel og godkjenning, slik resten
// av modellen gjør … Verdiene kontrolleres mot den innebygde fila, så ingenting kan skrives inn fritt. Alt skrives som forslag.»
//
// Tre deler:
// 1. ForhandRelasjonsmapping (ren funksjon): relasjonskoden i fila + hva navnet og motparten er → kategori, typekode og retning, eller grunnen til
//    at relasjonen ikke kan skrives (ingen kategori, eller tvetydig). Tabellen står i docs/33 §5.16.3.
// 2. AktorkoRelasjonTjeneste.ForslagAsync: slår opp nodene, hjemmelen og eksisterende kanter, og sier per relasjon hva knappen skriver, eller
//    hvorfor den er av («opprett navnet først») eller borte («kan ikke skrives: …»), eller at kanten alt finnes eller er avvist.
// 3. AktorkoRelasjonTjeneste.SkrivAsync: regner ut det samme på nytt og skriver gjennom StrukturkantTjeneste.OpprettAsync som manuelt_foreslatt.
//    Forespørselen har bare nummer, indeks og nodene brukeren så; alt annet kommer fra fila og basen.

/// <summary>[Ny, #469] Hva som skjer med en relasjon i forhåndsklassifiseringen. Lukket vokabular, speilet i frontenden.</summary>
public static class Relasjonsstatus
{
    /// <summary>Knappen skriver kanten som forslag.</summary>
    public const string KanSkrives = "kan_skrives";
    /// <summary>Navnet er ikke opprettet ennå. Knappen er av: «opprett navnet først».</summary>
    public const string OpprettNavnetForst = "opprett_navnet_forst";
    /// <summary>Motparten (den andre noden) finnes ikke ennå. Knappen er av.</summary>
    public const string OpprettMotpartenForst = "opprett_motparten_forst";
    /// <summary>En forutsetning i basen mangler (aktørtypen «ordning» for R forvaltes_av). Knappen er av; den gjettes ikke.</summary>
    public const string ManglerForutsetning = "mangler_forutsetning";
    /// <summary>En kant med samme kategori, type, fra og til finnes. Den skrives ikke på nytt.</summary>
    public const string Finnes = "finnes";
    /// <summary>En slik kant er avvist av et menneske. Den skrives ikke på nytt i stillhet.</summary>
    public const string Avvist = "avvist";
    /// <summary>Ingen kategori, tvetydig, eller fila mangler det kanten krever. Ingen knapp.</summary>
    public const string KanIkkeSkrives = "kan_ikke_skrives";
    /// <summary>Relasjonen er en navneform som «Koble til en aktør som finnes» skriver (#440).</summary>
    public const string SkrivesAvKoblingen = "skrives_av_koblingen";
}

/// <summary>[Ny, #469] Kategori, typekode og retning for en relasjon, eller grunnen til at den ikke kan skrives.</summary>
/// <param name="NavnetErFra">Navnet er fra-noden. False når kanten går motsatt vei av relasjonen i fila (I innehar, T skal_ha, M medlem_av fra
/// et medlem, O bestar_av fra det større området, R oppretter).</param>
public sealed record Kantmapping(string? Kategori, string? Typekode, bool NavnetErFra, string? Grunn)
{
    public bool HarKategori => Kategori is not null;
    public static Kantmapping Ikke(string grunn) => new(null, null, false, grunn);
}

/// <summary>[Ny, #469] Hva en node i relasjonen er: <c>aktor</c> (virksomhet) eller begrepskategorien (<c>klasse</c>, <c>rolle</c>, <c>omrade</c>).</summary>
public static class Nodeart
{
    public const string Aktor = "aktor";

    public static string Tekst(string? art) => art switch
    {
        Aktor => "en aktør",
        Nodetyper.Klasse => "en klasse",
        Nodetyper.Rolle => "en rolle",
        Nodetyper.Omrade => "et område",
        null => "ukjent",
        _ => art,
    };
}

/// <summary>
/// [Ny, issue #469] Mappingen fra relasjonskodene i <c>klassifisering.json</c> til strukturkanter (docs/33 §4.3 og §5.16.3). Ren funksjon.
/// <para>Regelen er at en relasjon skrives bare når koden, hva navnet er og hva motparten er, sammen gir ÉN kategori og ÉN typekode i modellen
/// slik den er dokumentert. Alt annet er «kan ikke skrives», med grunnen. En relasjon tolkes aldri ut av merknaden (fritekst).</para>
/// </summary>
public static class ForhandRelasjonsmapping
{
    /// <summary>Relasjonskodene som ikke er strukturkanter, og hvorfor. Står i tabellen i docs/33 §5.16.3.</summary>
    public static readonly IReadOnlyDictionary<string, string> IkkeKant = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["samme_som"] = "en navneform, ikke en strukturkant. «Koble til en aktør som finnes» skriver den når motparten er en aktør",
        ["utgatt_navn"] = "en navneform, ikke en strukturkant. «Koble til en aktør som finnes» skriver den",
        ["kortform_for"] = "en navneform (kortform), ikke en strukturkant. Den registreres som navneform på aktøren",
        ["omtale_av"] = "en binding av en generisk omtale (docs/33 §5.7), ikke en strukturkant. Fila har verken rettskilde, spenn eller hjemmel "
                        + "for bindingen; den registreres i én-skjermen for generisk omtale",
        ["generisk_omtale"] = "en generisk omtale uten referent, ikke en strukturkant",
        ["medlemsregel"] = "en medlemsregel, ikke en strukturkant. Medlemskapsregelen på en klasse er et lukket vokabular av utregnede regler "
                           + "(#384, #442); fila har regelen som tekst",
        ["personkrets_i"] = "navnet er ikke en aktør og får ingen node å skrive kanten fra",
        ["fores_av"] = "navnet (et register) er ikke en aktør og får ingen node å skrive kanten fra",
        ["fores_for"] = "navnet (et register) er ikke en aktør og får ingen node å skrive kanten fra",
        ["kan_bestemmes_av"] = "tvetydig: en kompetanse (K), men fila sier ikke hvilken type (oppretting, beslutning …)",
        ["ansvarlig"] = "tvetydig: «har ansvaret for» kan være plikt, oppgave eller kompetanse, og fila sier ikke hvilken",
        ["organ_for"] = "tvetydig: et felles organ for to klasser har ingen organtilhørighet med én målnode",
        ["underlagt"] = "tvetydig: administrativ eller faglig underordning, og fila sier ikke hvilken",
        ["venter"] = "venter på modellvalget (#463)",
    };

    /// <summary>
    /// Kategorien og typekoden for én relasjon. <paramref name="navnetsArt"/> er hva navnet er (fra filas type), <paramref name="motpartensArt"/>
    /// hva motparten er (fra basen når den har id, ellers fra fila), og <paramref name="antallRolleFor"/> hvor mange «rolle for» posten har.
    /// </summary>
    public static Kantmapping Kartlegg(string relasjon, string? navnetsArt, string? motpartensArt, int antallRolleFor)
    {
        if (IkkeKant.TryGetValue(relasjon, out var ikke)) return Kantmapping.Ikke(ikke);
        var navn = navnetsArt;
        var mot = motpartensArt;
        Kantmapping Feil(string forventet) => Kantmapping.Ikke(
            $"«{Forhandsklassifisering.Relasjonstekst(relasjon).ToLowerInvariant()}» skrives bare {forventet}; her er navnet {Nodeart.Tekst(navn)} og motparten {Nodeart.Tekst(mot)}");
        return relasjon switch
        {
            // I: aktør eller klasse (lest distributivt, #418) innehar rollen. Kanten går fra innehaveren til rollen.
            "rolle_innehas_av" => navn == Nodetyper.Rolle && mot is Nodeart.Aktor or Nodetyper.Klasse
                ? new(Strukturkanter.Rolleinnehav, Strukturkanter.Innehar, false, null)
                : Feil("fra en rolle til en aktør eller en klasse (I innehar)"),
            // Rolle for en klasse: T skal_ha (hvert medlem har rollen, docs/33 §4.3). Rolle for ett organ: G del_av (stillingen hører til organet,
            // #355 punkt 7) — bare når rollen er rolle for nettopp det ene organet. En generell rolle («dommer») hos flere er ikke del av ett.
            "rolle_for" => navn != Nodetyper.Rolle ? Feil("fra en rolle")
                : mot == Nodetyper.Klasse ? new(Strukturkanter.Klasseniva, "skal_ha", false, null)
                : mot == Nodeart.Aktor && antallRolleFor == 1 ? new(Strukturkanter.Organtilhorighet, "del_av", true, null)
                : mot == Nodeart.Aktor ? Kantmapping.Ikke($"tvetydig: rollen er «rolle for» {antallRolleFor} noder i fila. G del_av binder en stilling "
                                                           + "til ett organ (docs/33 §4.3, #355 punkt 7), og en rolle hos flere er ikke del av ett av dem")
                : Feil("fra en rolle til en klasse (T skal_ha) eller en aktør (G del_av)"),
            // Organ i en klasse: T skal_ha («hver kommune skal ha et kommunestyre», docs/33 §4.3). Organ i en aktør: G har_organ (er organ for).
            "organ_i" => navn == Nodetyper.Klasse && mot == Nodetyper.Klasse ? new(Strukturkanter.Klasseniva, "skal_ha", false, null)
                : navn == Nodeart.Aktor && mot == Nodeart.Aktor ? new(Strukturkanter.Organtilhorighet, "har_organ", true, null)
                : Feil("fra en klasse til en klasse (T skal_ha) eller fra en aktør til en aktør (G har_organ)"),
            // Klasse i klasse er M medlem_av (gruppe av gruppe, #164; klasse i klasse, #418).
            "underklasse_av" => navn == Nodetyper.Klasse && mot == Nodetyper.Klasse
                ? new(Strukturkanter.Medlemskap, Strukturkanter.MedlemAv, true, null)
                : Feil("fra en klasse til en klasse (M medlem_av)"),
            "medlem" => navn == Nodetyper.Klasse && mot == Nodeart.Aktor
                ? new(Strukturkanter.Medlemskap, Strukturkanter.MedlemAv, false, null)
                : Feil("fra en klasse til en aktør (M medlem_av fra aktøren)"),
            "bestar_av" => navn == Nodetyper.Omrade && mot == Nodetyper.Omrade
                ? new(Strukturkanter.Omradesammensetning, "bestar_av", true, null)
                : Feil("fra et område til et område (O bestar_av)"),
            "inngar_i" => navn == Nodetyper.Omrade && mot == Nodetyper.Omrade
                ? new(Strukturkanter.Omradesammensetning, "bestar_av", false, null)
                : Feil("fra et område til et område (O bestar_av fra det større)"),
            "har_ansvarsomrade" => navn == Nodeart.Aktor && mot == Nodetyper.Omrade
                ? new(Strukturkanter.Ansvarsomrade, "har_ansvarsomrade", true, null)
                : Feil("fra en aktør til et område (A har_ansvarsomrade)"),
            // Klageinstans er K klage med motparten som til-node (#341, docs/33 §4.3).
            "klageinstans_for" => navn == Nodeart.Aktor && mot == Nodeart.Aktor
                ? new(Strukturkanter.Kompetanse, "klage", true, null)
                : Feil("fra en aktør til en aktør (K klage)"),
            "forvaltes_av" => navn == Nodeart.Aktor && mot == Nodeart.Aktor
                ? new(Strukturkanter.Relasjon, Strukturkanter.ForvaltesAv, true, null)
                : Feil("fra en ordning til en aktør (R forvaltes_av)"),
            "opprettet_av" => navn == Nodeart.Aktor && mot == Nodeart.Aktor
                ? new(Strukturkanter.Relasjon, "oppretter", false, null)
                : Feil("mellom to aktører (R oppretter)"),
            "administrativt_underlagt" => navn == Nodeart.Aktor && mot == Nodeart.Aktor
                ? new(Strukturkanter.Relasjon, "administrativt_underordnet", true, null)
                : Feil("mellom to aktører (R administrativt_underordnet)"),
            "eies_av" => navn == Nodeart.Aktor && mot == Nodeart.Aktor
                ? new(Strukturkanter.Relasjon, "eies_av", true, null)
                : Feil("mellom to aktører (R eies_av)"),
            "sekretariat" => navn == Nodeart.Aktor && mot == Nodeart.Aktor
                ? new(Strukturkanter.Relasjon, "sekretariat_for", false, null)
                : Feil("mellom to aktører (R sekretariat_for)"),
            _ => Kantmapping.Ikke($"ingen kategori for relasjonen «{relasjon}» i modellen (docs/33 §4.3)"),
        };
    }

    /// <summary>Hva navnet er, fra filas type. Null for omtale, ikke aktør og venter: de får ingen egen node.</summary>
    public static string? NavnetsArt(ForhandPost p) => p.VenterPaModellvalg ? null : Forhandstype.FraFil(p.Type) switch
    {
        Forhandstype.Aktor => Nodeart.Aktor,
        Forhandstype.Klasse => Nodetyper.Klasse,
        Forhandstype.Rolle => Nodetyper.Rolle,
        Forhandstype.Omrade => Nodetyper.Omrade,
        _ => null,
    };

    /// <summary>Typen til en ny node, fra posten som har den som navn eller fra tilleggsnodene. Null når den ikke står, eller er tvetydig.</summary>
    public static string? ArtForNyNode(string navn)
    {
        var arter = Forhandsklassifisering.Alle.Where(p => p.NyNode == navn).Select(NavnetsArt)
            .Concat(Forhandsklassifisering.Tilleggsnoder.Where(t => t.NyNode == navn).Select(t => NavnetsArt(new ForhandPost { Type = t.Type })))
            .Where(a => a is not null).Distinct().ToList();
        return arter is [var eneste] ? eneste : null;
    }

    /// <summary>Hva motparten er når basen ikke er spurt: filas «kategori» for en id (virksomhet → aktør), eller typen til en ny node.</summary>
    public static string? MotpartensArtIFila(JsonElement? til)
    {
        if (Forhandsklassifisering.Felt(til, "ny_node") is { } ny) return ArtForNyNode(ny);
        return Forhandsklassifisering.Felt(til, "kategori") switch
        {
            "virksomhet" => Nodeart.Aktor,
            Nodetyper.Klasse => Nodetyper.Klasse,
            Nodetyper.Rolle => Nodetyper.Rolle,
            Nodetyper.Omrade => Nodetyper.Omrade,
            _ => Forhandsklassifisering.Felt(til, "tabell") == "virksomheter" ? Nodeart.Aktor : null,
        };
    }

    /// <summary>Merket modellereren bruker i fila for en slutning («[slutning]», «[slutning: …]»). En slutning bygger ikke på navnets hjemmel
    /// alene; den er avledet og krever premisser, som fila ikke har. Den skrives derfor ikke med navnets hjemmel.</summary>
    public static bool ErSlutning(ForhandRelasjonFil r) => (r.Merknad ?? "").Contains("[slutning", StringComparison.Ordinal);
}

// ------------------------------------------------------------------------------------------------ Det API-et viser

/// <summary>En ende av kanten slik knappen viser den.</summary>
/// <param name="Id">Noden i basen. Null når den ikke finnes ennå.</param>
/// <param name="Type"><c>virksomhet</c> eller <c>begrep</c> når den finnes.</param>
/// <param name="Art"><see cref="Nodeart"/>.</param>
/// <param name="ErNavnet">Enden er navnet i køen (ikke motparten).</param>
/// <param name="Merknad">Hvorfor noden ikke er funnet, når den ikke er det.</param>
public sealed record RelasjonNode(Guid? Id, string? Type, string Navn, string? Art, bool ErNavnet, string? Merknad = null);

/// <summary>Hjemmelen kanten får: navnets hjemmel i fila, løst til bestemmelsen i korpus.</summary>
public sealed record RelasjonHjemmel(Guid RettskildeId, string Rettskilde, string Eid, string? Bestemmelse, string? Sitat);

/// <summary>
/// [Ny, #469] Én relasjon i forhåndsklassifiseringen og hva knappen gjør med den. Alt som skrives, står her: fra, til, kategori, type, hjemmel,
/// kunnskapsstatus og status (alltid <c>manuelt_foreslatt</c>).
/// </summary>
/// <param name="Indeks">Relasjonens plass i postens liste (0-basert). Nøkkelen sammen med <paramref name="Nr"/>.</param>
/// <param name="Utsagn">Kanten som setning: «kommune innehar rollen bostedskommune». Null når den ikke kan skrives.</param>
/// <param name="KantId">Kanten som finnes eller er avvist.</param>
public sealed record ForhandRelasjonsforslag(
    int Nr, int Indeks, string Relasjon, string Relasjonstekst, string? Merknad, string Status, string? Grunn,
    string? Kategori, string? Kategoritekst, string? Typekode, string? Utsagn,
    RelasjonNode? Fra, RelasjonNode? Til, RelasjonHjemmel? Hjemmel, string? Kunnskapsstatus,
    IReadOnlyList<string>? Tolkningsargumenter, string? Tolkningsbegrunnelse, string SkrivesSom,
    Guid? KantId = null, string? KantStatus = null, string? KantSistEndretAv = null, string? KantAvvisningBegrunnelse = null);

/// <summary>[Ny, #469] Relasjonene for ett navn i fila.</summary>
/// <param name="NavnetsNode">Navnets node når den finnes (navnet er opprettet).</param>
public sealed record ForhandRelasjoner(int Nr, string Navn, string Type, string? NyNode, RelasjonNode? NavnetsNode,
    IReadOnlyList<ForhandRelasjonsforslag> Relasjoner);

/// <summary>[Ny, #469] Det brukeren så. Er nodene andre nå, avvises skrivingen (409): ingen skriver noe annet enn det som ble vist.</summary>
public sealed record ForhandRelasjonSkriv(Guid? FraId, Guid? TilId);

/// <summary>
/// [Ny, issue #469] Slår opp og skriver forhåndsklassifiseringens relasjoner som strukturkanter. Skrivingen går gjennom
/// <see cref="StrukturkantTjeneste.OpprettAsync"/> (den eneste skriveveien for kanter) med status <c>manuelt_foreslatt</c>.
/// </summary>
public sealed class AktorkoRelasjonTjeneste(RegelIdeDbContext db, StrukturkantTjeneste kanter)
{
    /// <summary>Oppdagelseskilden. «manuell»: et menneske har sett og bekreftet hver kant. Opphavet (fil, nummer, relasjon) står i begrunnelsen.
    /// Ikke <c>fasit:</c>, fordi det gir merket «Fra fasiten, faglig vurdert» (#387), og forhåndsklassifiseringen er ikke fasit.</summary>
    public const string Oppdagelseskilde = "manuell";

    // ------------------------------------------------------------------------------------------------ Lesing

    /// <summary>Relasjonene for ett navn (nummeret i fila). Null når nummeret ikke finnes.</summary>
    public async Task<ForhandRelasjoner?> ForNrAsync(int nr, CancellationToken ct = default)
    {
        var p = Forhandsklassifisering.Alle.FirstOrDefault(x => x.Nr == nr);
        if (p is null) return null;
        return (await ByggAsync([p], ct)).Single();
    }

    /// <summary>Alle navn med relasjoner. <paramref name="bareOpprettede"/>: bare navnene som er opprettet (navnets node finnes), som er de
    /// som ikke lenger står i køen etter godkjenningen.</summary>
    public async Task<IReadOnlyList<ForhandRelasjoner>> AlleAsync(bool bareOpprettede, CancellationToken ct = default)
    {
        var poster = Forhandsklassifisering.Alle.Where(p => p.Relasjoner.Count > 0).ToList();
        var alle = await ByggAsync(poster, ct);
        return bareOpprettede ? alle.Where(r => r.NavnetsNode?.Id is not null).ToList() : alle;
    }

    // ------------------------------------------------------------------------------------------------ Skriving

    /// <summary>
    /// Skriver relasjonen <paramref name="indeks"/> for navnet <paramref name="nr"/> som forslag. Regner ut forslaget på nytt fra fila og basen;
    /// er det ikke <see cref="Relasjonsstatus.KanSkrives"/>, skrives ingenting. Null når nummeret eller indeksen ikke finnes.
    /// </summary>
    /// <exception cref="AktorkoKonfliktException">Kanten finnes eller er avvist, eller nodene er andre enn brukeren så.</exception>
    /// <exception cref="ArgumentException">Relasjonen kan ikke skrives (grunnen står i meldingen), eller strukturkanttjenesten avviser den.</exception>
    public async Task<ForhandRelasjonsforslag?> SkrivAsync(int nr, int indeks, ForhandRelasjonSkriv sett, string bruker, CancellationToken ct = default)
    {
        var relasjoner = await ForNrAsync(nr, ct);
        if (relasjoner is null || indeks < 0 || indeks >= relasjoner.Relasjoner.Count) return null;
        var f = relasjoner.Relasjoner[indeks];
        if (f.Status is Relasjonsstatus.Finnes or Relasjonsstatus.Avvist)
        {
            throw new AktorkoKonfliktException($"Kanten {(f.Status == Relasjonsstatus.Avvist ? "er avvist" : "finnes alt")}; den skrives ikke på nytt.");
        }
        if (f.Status != Relasjonsstatus.KanSkrives)
        {
            throw new ArgumentException($"Relasjonen kan ikke skrives: {f.Grunn}");
        }
        if (sett.FraId != f.Fra!.Id || sett.TilId != f.Til!.Id)
        {
            throw new AktorkoKonfliktException("Nodene er endret siden du så forslaget. Les det på nytt; ingenting er skrevet.");
        }
        var p = Forhandsklassifisering.Alle.Single(x => x.Nr == nr);
        var r = p.Relasjoner[indeks];
        var h = f.Hjemmel!;
        var begrunnelse = $"Relasjon fra forhåndsklassifiseringen ({Forhandsklassifisering.Kilde}, n{nr}, relasjon {indeks + 1}: "
                          + $"«{relasjoner.Navn}» {f.Relasjonstekst} «{f.Til!.Navn}»{(f.Til.ErNavnet ? $" / «{f.Fra.Navn}»" : "")}; KI-agent og juristrunde, "
                          + $"#440/#469). Hjemmelen er navnets hjemmel i fila{(h.Sitat is { } s ? $": «{s}»" : "")}."
                          + (string.IsNullOrWhiteSpace(r.Merknad) ? "" : $" Merknad i fila: {r.Merknad}.")
                          + (f.Tolkningsargumenter is not null && f.Tolkningsbegrunnelse is { } tb ? $" Tolkningsargumenter: {tb}" : "");
        var ny = new NyStrukturkant(
            f.Kategori!, f.Typekode!, Ende(f.Fra), Ende(f.Til),
            HjemmelRettskildeId: h.RettskildeId, HjemmelEid: h.Eid,
            Status: Strukturkanter.ManueltForeslatt, OppdagelsesKilde: Oppdagelseskilde,
            Kunnskapsstatus: f.Kunnskapsstatus, Begrunnelse: begrunnelse, Tolkningsargumenter: f.Tolkningsargumenter);
        var opprettet = await kanter.OpprettAsync(ny, bruker, ct);
        return f with
        {
            Status = Relasjonsstatus.Finnes, Grunn = opprettet.VarNy ? null : "fantes alt (identisk kant)", KantId = opprettet.Kant.Id,
            KantStatus = opprettet.Kant.Status,
        };
    }

    private static Kantnode Ende(RelasjonNode n) => n.Type == "virksomhet" ? Kantnode.Virksomhet(n.Id!.Value) : Kantnode.Begrep(n.Id!.Value);

    // ------------------------------------------------------------------------------------------------ Oppslaget

    private sealed record Funnet(Guid Id, string Type, string Navn, string? Art);

    private async Task<List<ForhandRelasjoner>> ByggAsync(IReadOnlyList<ForhandPost> poster, CancellationToken ct)
    {
        // 1. Nodene med id i fila.
        var ider = poster.SelectMany(Forhandsklassifisering.NodeIder).Distinct().ToList();
        var medId = new Dictionary<Guid, Funnet>();
        foreach (var v in await db.Virksomheter.Where(v => ider.Contains(v.Id)).Select(v => new { v.Id, v.Navn }).ToListAsync(ct))
        {
            medId[v.Id] = new Funnet(v.Id, "virksomhet", v.Navn, Nodeart.Aktor);
        }
        foreach (var b in await db.Begreper.Where(b => ider.Contains(b.Id)).Select(b => new { b.Id, b.Term, b.Begrepskategori }).ToListAsync(ct))
        {
            medId.TryAdd(b.Id, new Funnet(b.Id, "begrep", b.Term, b.Begrepskategori));
        }

        // 2. Nodene med navn: navnene selv og de nye nodene relasjonene peker på. Slått opp på navnet i fila, uten forskjell på store og små
        //    bokstaver. Aktør: en gjeldende navneform med termen (CLAUDE.md §0: lovens navn, ikke registernavnet). Klasse, rolle, område: et
        //    gjeldende begrep med kategorien og termen.
        var navn = poster.Select(p => p.NyNode)
            .Concat(poster.SelectMany(p => p.Relasjoner).Select(r => Forhandsklassifisering.Felt(r.Til, "ny_node")))
            .Where(x => x is not null).Select(x => x!.ToLowerInvariant()).Distinct().ToList();
        var navneformer = await db.Begreper
            .Where(b => b.VirksomhetId != null && navn.Contains(b.Term.ToLower()) && b.Entitetsstatus == "gjeldende"
                        && b.Godkjenning != Strukturkanter.Avvist && b.Status != "arkivert" && b.Status != "tilbaketrukket")
            .Join(db.Virksomheter.Where(v => v.ErstattetAvId == null), b => b.VirksomhetId, v => v.Id, (b, v) => new { b.Term, v.Id, v.Navn, v.Aktortype })
            .ToListAsync(ct);
        var begreper = await db.Begreper
            .Where(b => b.VirksomhetId == null && b.Begrepskategori != null && navn.Contains(b.Term.ToLower()) && b.Entitetsstatus == "gjeldende"
                        && (b.Godkjenning == null || b.Godkjenning != Strukturkanter.Avvist) && b.Status != "arkivert" && b.Status != "tilbaketrukket")
            .Select(b => new { b.Id, b.Term, b.Begrepskategori, b.LovkildeId }).ToListAsync(ct);
        var aktortyper = await db.Virksomheter.Where(v => ider.Contains(v.Id)).Select(v => new { v.Id, v.Aktortype }).ToDictionaryAsync(v => v.Id, v => v.Aktortype, ct);
        foreach (var n in navneformer) aktortyper[n.Id] = n.Aktortype;

        // 3. Hjemlene: bestemmelsene filas eId-er står i, og rettskildene der navnet står (for en eId som står i flere rettskilder).
        var eids = poster.Select(p => p.Hjemmel?.Eid).Where(e => e is not null).Select(e => e!).Distinct().ToList();
        var noder = await db.RettskildeNoder.Where(n => eids.Contains(n.Eid) && n.Entitetsstatus == "gjeldende")
            .Select(n => new { n.RettskildeId, n.Eid, n.Tekst, n.ErMerknad }).ToListAsync(ct);
        var ord = poster.SelectMany(p => p.Varianter.Select(v => v.Normalisert)).Distinct().ToList();
        var navnetsKilder = (await db.Navnekandidater.Where(k => ord.Contains(k.ForeslattTekst.ToLower()))
                .Select(k => new { Tekst = k.ForeslattTekst.ToLower(), k.RettskildeId }).Distinct().ToListAsync(ct))
            .ToLookup(x => x.Tekst, x => x.RettskildeId);
        var rettskildeIder = noder.Select(n => n.RettskildeId).Distinct().ToList();
        var titler = await db.Rettskilder.Where(r => rettskildeIder.Contains(r.Id)).Select(r => new { r.Id, Tittel = r.Kortnavn ?? r.Tittel })
            .ToDictionaryAsync(r => r.Id, r => r.Tittel, ct);

        (RelasjonHjemmel? Hjemmel, string? Feil) Hjemmel(ForhandPost p)
        {
            if (p.Hjemmel is not { } h) return (null, "fila har ingen hjemmel for navnet, og kanten krever hjemmel");
            var treff = noder.Where(n => n.Eid == h.Eid).ToList();
            var kilder = treff.Select(t => t.RettskildeId).Distinct().ToList();
            var navnets = kilder.Where(k => p.Varianter.Any(v => navnetsKilder[v.Normalisert].Contains(k))).ToList();
            // Samme regel som AktorkoTjeneste.ForhandBestemmelseAsync: filas rettskilde, den ene med eId-en, eller den ene der navnet står.
            Guid? valgt = h.RettskildeId is { } fra && kilder.Contains(fra) ? fra : kilder.Count == 1 ? kilder[0] : navnets.Count == 1 ? navnets[0] : null;
            if (valgt is not { } rid)
            {
                return (null, kilder.Count == 0
                    ? $"hjemmelen ({h.Eid}) står ikke i korpus, og fila har ingen kilde utenfor korpus"
                    : $"hjemmelens eId står i {kilder.Count} rettskilder, og fila sier ikke hvilken; den gjettes ikke");
            }
            var node = treff.First(t => t.RettskildeId == rid);
            if (node.ErMerknad) return (null, "hjemmelen er en merknad eller veiledning, som aldri er hjemmel (#419)");
            return (new RelasjonHjemmel(rid, titler.GetValueOrDefault(rid, rid.ToString()), h.Eid, AktorkoTjeneste.Bestemmelse(h.Eid),
                string.IsNullOrWhiteSpace(h.Sitat) ? node.Tekst : h.Sitat), null);
        }

        // En node med navn: nøyaktig én. Flere begreper med samme term (lovspesifikke) snevres inn til det i hjemmelens rettskilde.
        RelasjonNode NodeMedNavn(string nodenavn, string? art, bool erNavnet, Guid? foretrukketLov)
        {
            if (art is null) return new RelasjonNode(null, null, nodenavn, null, erNavnet, "typen står ikke i fila; noden slås ikke opp uten å gjette");
            var lav = nodenavn.ToLowerInvariant();
            if (art == Nodeart.Aktor)
            {
                var v = navneformer.Where(n => n.Term.ToLower() == lav).Select(n => (n.Id, n.Navn)).Distinct().ToList();
                return v switch
                {
                    [var en] => new RelasjonNode(en.Id, "virksomhet", nodenavn, art, erNavnet),
                    [] => new RelasjonNode(null, null, nodenavn, art, erNavnet, "ingen aktør har navneformen ennå"),
                    _ => new RelasjonNode(null, null, nodenavn, art, erNavnet, $"tvetydig: {v.Count} aktører har navneformen"),
                };
            }
            var b = begreper.Where(x => x.Term.ToLower() == lav && x.Begrepskategori == art).ToList();
            if (b.Count > 1 && foretrukketLov is { } lov && b.Count(x => x.LovkildeId == lov) == 1) b = b.Where(x => x.LovkildeId == lov).ToList();
            return b switch
            {
                [var en] => new RelasjonNode(en.Id, "begrep", en.Term, art, erNavnet),
                [] => new RelasjonNode(null, null, nodenavn, art, erNavnet, $"{Nodeart.Tekst(art)} med navnet finnes ikke ennå"),
                _ => new RelasjonNode(null, null, nodenavn, art, erNavnet, $"tvetydig: {b.Count} begreper heter «{nodenavn}» ({Nodeart.Tekst(art)})"),
            };
        }

        // 4. Navnenes noder og hjemler, og motpartene.
        var resultat = new List<(ForhandPost Post, RelasjonNode? Navnet, List<(ForhandRelasjonFil R, RelasjonNode? Mot, Kantmapping M)> Rel,
            (RelasjonHjemmel? Hjemmel, string? Feil) H)>();
        foreach (var p in poster)
        {
            var h = Hjemmel(p);
            var art = ForhandRelasjonsmapping.NavnetsArt(p);
            var navnet = art is not null && p.NyNode is { } nn ? NodeMedNavn(nn, art, true, h.Hjemmel?.RettskildeId) : null;
            var antallRolleFor = p.Relasjoner.Count(r => r.Relasjon == "rolle_for");
            var rel = new List<(ForhandRelasjonFil, RelasjonNode?, Kantmapping)>();
            foreach (var r in p.Relasjoner)
            {
                RelasjonNode? mot = null;
                string? motArt;
                if (Forhandsklassifisering.Felt(r.Til, "id") is { } idTekst && Guid.TryParse(idTekst, out var id))
                {
                    if (medId.TryGetValue(id, out var f)) { mot = new RelasjonNode(f.Id, f.Type, f.Navn, f.Art, false); motArt = f.Art; }
                    else
                    {
                        motArt = ForhandRelasjonsmapping.MotpartensArtIFila(r.Til);
                        mot = new RelasjonNode(null, null, Forhandsklassifisering.Felt(r.Til, "navn") ?? idTekst, motArt, false,
                            "id-en i fila finnes ikke i basen");
                    }
                }
                else if (Forhandsklassifisering.Felt(r.Til, "ny_node") is { } ny)
                {
                    motArt = ForhandRelasjonsmapping.ArtForNyNode(ny);
                    var motpost = Forhandsklassifisering.Alle.FirstOrDefault(x => x.NyNode == ny && x.Hjemmel is not null);
                    mot = NodeMedNavn(ny, motArt, false, motpost is null ? null : Hjemmel(motpost).Hjemmel?.RettskildeId);
                }
                else
                {
                    motArt = null;
                    if (Forhandsklassifisering.Felt(r.Til, "tekst") is { } tekst) mot = new RelasjonNode(null, null, tekst, null, false, "målet er tekst, ikke en node");
                }
                rel.Add((r, mot, ForhandRelasjonsmapping.Kartlegg(r.Relasjon, art, motArt, antallRolleFor)));
            }
            resultat.Add((p, navnet, rel, h));
        }

        // 5. Kantene som finnes (også avviste) mellom nodene.
        var nodeIder = resultat.SelectMany(x => x.Rel.Select(r => r.Mot?.Id).Append(x.Navnet?.Id)).Where(i => i is not null).Select(i => i!.Value)
            .Distinct().ToList();
        var finnes = await kanter.GjeldendeInklAvviste
            .Where(k => (k.FraVirksomhetId != null && nodeIder.Contains(k.FraVirksomhetId.Value))
                        || (k.FraBegrepId != null && nodeIder.Contains(k.FraBegrepId.Value)))
            .Select(k => new { k.Id, k.Kategori, k.Typekode, Fra = k.FraVirksomhetId ?? k.FraBegrepId, Til = k.TilVirksomhetId ?? k.TilBegrepId, k.Status,
                k.SistEndretAv, k.AvvisningBegrunnelse })
            .ToListAsync(ct);

        return resultat.Select(x =>
        {
            var p = x.Post;
            var handling = Forhandsklassifisering.Handling(p);
            var kobling = handling == Forhandshandling.Koble ? Forhandsklassifisering.Koblingsrelasjon(p) : null;
            var liste = x.Rel.Select((rel, i) =>
            {
                var (r, mot, m) = rel;
                var tekst = Forhandsklassifisering.Relasjonstekst(r.Relasjon).ToLowerInvariant();
                ForhandRelasjonsforslag Uten(string status, string grunn) => new(p.Nr, i, r.Relasjon, tekst, Nullhvis(r.Merknad), status, grunn,
                    null, null, null, null, null, mot, null, null, null, null, Strukturkanter.ManueltForeslatt);

                if (kobling is not null && ReferenceEquals(r, kobling))
                {
                    return Uten(Relasjonsstatus.SkrivesAvKoblingen, "koblingen gjør navnet til navneform på aktøren");
                }
                if (p.VenterPaModellvalg) return Uten(Relasjonsstatus.KanIkkeSkrives, "navnet venter på modellvalget (#463)");
                // Rekkefølgen på grunnene: koden først (ikke en kant), så motparten (tekst, ingen, ukjent type), så nodeartene.
                if (ForhandRelasjonsmapping.IkkeKant.ContainsKey(r.Relasjon)) return Uten(Relasjonsstatus.KanIkkeSkrives, m.Grunn!);
                if (mot is null || (mot.Id is null && mot.Merknad == "målet er tekst, ikke en node"))
                {
                    return Uten(Relasjonsstatus.KanIkkeSkrives, mot is null ? "relasjonen har ingen motpart i fila" : $"målet er tekst («{mot.Navn}»), ikke en node");
                }
                if (mot.Art is null) return Uten(Relasjonsstatus.KanIkkeSkrives, $"motparten «{mot.Navn}»: {mot.Merknad ?? "typen er ukjent"}");
                if (!m.HarKategori) return Uten(Relasjonsstatus.KanIkkeSkrives, m.Grunn!);
                if (ForhandRelasjonsmapping.ErSlutning(r))
                {
                    return Uten(Relasjonsstatus.KanIkkeSkrives, "relasjonen er merket som slutning i fila. Den bygger ikke på navnets hjemmel alene, og en "
                                                                 + "avledet kant krever premisser fila ikke har");
                }
                if (x.H.Hjemmel is not { } hjemmel) return Uten(Relasjonsstatus.KanIkkeSkrives, x.H.Feil!);
                var ks = p.Hjemmel!.Kunnskapsstatus;
                if (ks is not (Strukturkanter.Tekstfunn or Strukturkanter.Normalisering or Strukturkanter.Fortolkning))
                {
                    return Uten(Relasjonsstatus.KanIkkeSkrives, $"hjemmelens kunnskapsstatus i fila («{ks ?? "ingen"}») kan ikke brukes for en kant med hjemmel i korpus");
                }
                var argumenter = ks == Strukturkanter.Fortolkning ? p.Hjemmel.Tolkningsargumenter : null;
                if (ks == Strukturkanter.Fortolkning && argumenter is not { Count: > 0 })
                {
                    return Uten(Relasjonsstatus.KanIkkeSkrives, "hjemmelen er fortolkning, men fila har ingen tolkningsargumenter for den; de velges ikke fritt");
                }

                var navnet = x.Navnet!;
                var (fra, til) = m.NavnetErFra ? (navnet, mot) : (mot, navnet);
                var regel = Strukturkanter.Noderegler[m.Kategori!];
                var forslag = new ForhandRelasjonsforslag(p.Nr, i, r.Relasjon, tekst, Nullhvis(r.Merknad), Relasjonsstatus.KanSkrives, null,
                    m.Kategori, Strukturkanter.Visningsnavn(m.Kategori!), m.Typekode, Utsagn(m.Kategori!, m.Typekode!, fra.Navn, til.Navn),
                    fra, til, hjemmel, ks, argumenter, argumenter is null ? null : p.Hjemmel.Tolkningsbegrunnelse, Strukturkanter.ManueltForeslatt);

                if (navnet.Id is null)
                {
                    return forslag with
                    {
                        Status = navnet.Merknad?.StartsWith("tvetydig") == true ? Relasjonsstatus.KanIkkeSkrives : Relasjonsstatus.OpprettNavnetForst,
                        Grunn = navnet.Merknad?.StartsWith("tvetydig") == true ? $"navnets node er {navnet.Merknad}" : "opprett navnet først (godkjenn det i køen)",
                    };
                }
                if (mot.Id is null)
                {
                    // En id i fila som ikke finnes i basen, er ikke noe å opprette: forslaget må kontrolleres.
                    var ikke = mot.Merknad?.StartsWith("tvetydig") == true || mot.Merknad == "id-en i fila finnes ikke i basen";
                    return forslag with
                    {
                        Status = ikke ? Relasjonsstatus.KanIkkeSkrives : Relasjonsstatus.OpprettMotpartenForst,
                        Grunn = ikke ? $"motparten «{mot.Navn}»: {mot.Merknad}" : $"opprett «{mot.Navn}» først ({mot.Merknad})",
                    };
                }
                // Motparten kan være noe annet i basen enn fila sier (en id til en klasse som er blitt en gruppe): kontroller mot noderegelen.
                if ((NoderegelFeil(regel, fra, true) ?? NoderegelFeil(regel, til, false)) is { } feil)
                {
                    return forslag with { Status = Relasjonsstatus.KanIkkeSkrives, Grunn = feil };
                }
                if (Strukturkanter.KreverOrdningSomFra(m.Kategori!, m.Typekode!) && aktortyper.GetValueOrDefault(fra.Id!.Value) != Nodetyper.Ordning)
                {
                    return forslag with
                    {
                        Status = Relasjonsstatus.ManglerForutsetning,
                        Grunn = $"sett aktørtypen «ordning» på «{fra.Navn}» først (aktørsiden); den gjettes ikke (#353)",
                    };
                }
                var eksisterende = finnes.Where(k => k.Kategori == m.Kategori && k.Typekode == m.Typekode && k.Fra == fra.Id && k.Til == til.Id)
                    .OrderBy(k => k.Status == Strukturkanter.Avvist ? 1 : 0).FirstOrDefault();
                if (eksisterende is not null)
                {
                    var avvist = eksisterende.Status == Strukturkanter.Avvist;
                    return forslag with
                    {
                        Status = avvist ? Relasjonsstatus.Avvist : Relasjonsstatus.Finnes,
                        Grunn = avvist ? "kanten er avvist; den skrives ikke på nytt" : "kanten finnes; den skrives ikke på nytt",
                        KantId = eksisterende.Id, KantStatus = eksisterende.Status, KantSistEndretAv = eksisterende.SistEndretAv,
                        KantAvvisningBegrunnelse = eksisterende.AvvisningBegrunnelse,
                    };
                }
                return forslag;
            }).ToList();
            return new ForhandRelasjoner(p.Nr, p.Navn, Forhandsklassifisering.Type(p), p.NyNode, x.Navnet, liste);
        }).ToList();
    }

    private static string? Nullhvis(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    /// <summary>Kanten som setning, med typekonfigurasjonens fra-mal: «kommune innehar rollen bostedskommune». K: «… har klagekompetanse overfor …».</summary>
    public static string Utsagn(string kategori, string typekode, string fra, string til)
    {
        var mal = Strukturkanter.Startsett.FirstOrDefault(s => s.Kategori == kategori && s.Kode == typekode).FraMal ?? $"{typekode} {{0}}";
        return $"{fra} {string.Format(mal, kategori == Strukturkanter.Kompetanse ? $"overfor {til}" : til)}";
    }

    private static string? NoderegelFeil(Strukturkanter.Noderegel regel, RelasjonNode n, bool erFra)
    {
        var (virksomhet, begrep, beskrivelse) = erFra ? (regel.FraVirksomhet, regel.FraBegrep, regel.FraBeskrivelse) : (regel.TilVirksomhet, regel.TilBegrep, regel.TilBeskrivelse);
        var lovlig = n.Type == "virksomhet" ? virksomhet : n.Art is { } a && begrep.Contains(a);
        return lovlig ? null : $"«{n.Navn}» er {Nodeart.Tekst(n.Art)} i basen, og {(erFra ? "fra" : "til")}-noden må være {beskrivelse}";
    }
}
