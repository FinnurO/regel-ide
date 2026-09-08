using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, gruppemedlemskap-runden, 2026-09-08, issue #164] Seeder Johanns eget «gruppe av gruppe»-
/// eksempel som EKTE data: forvaltningsområdet for samiske språk, med de tre kommunekategoriene som
/// MEDLEMSGRUPPER og de 14 navngitte kommunene som konkrete medlemmer.
///
/// <para>
/// <b>Bygget på de generelle tjenestene, ikke som et engangs-script</b> (issue #164 sitt
/// akseptansekriterium 5): hver rad går gjennom <see cref="VirksomhetsbegrepTjeneste"/>,
/// <see cref="GruppeMedlemskapTjeneste"/>, <see cref="MyndighetstildelingTjeneste"/> og
/// <see cref="TekstTaggTjeneste"/> — samme kodevei en saksbehandler utløser fra veiviseren. Seeden er
/// derfor samtidig en verifikasjon av at mekanismen virker på et ekte tilfelle; ville den bare skrevet
/// rader direkte med <c>db.X.Add</c>, hadde den kunnet «lykkes» selv om mekanismen var ødelagt.
/// </para>
///
/// <para>
/// <b>Modellen eksempelet illustrerer.</b> Sameloven § 3-1 første ledd punkt 1 DEFINERER
/// gruppekonseptene («kommuner inndelt i kategoriene språkutviklingskommuner,
/// språkvitaliseringskommuner …») uten å navngi én kommune. En SEPARAT forskrift § 1 navngir så
/// medlemmene. Karasjok får dermed sine plikter etter sameloven INDIREKTE: den er navngitt som medlem
/// av «språkutviklingskommuner» i forskriften, og den gruppen er selv medlem av
/// «forvaltningsområdet for samiske språk», som er det loven refererer til. Derfor:
/// gruppebegrepene hjemles i LOVEN (<c>LovkildeId</c>), mens både medlemskapene mellom grupper og
/// tildelingene ned til kommunene hjemles i FORSKRIFTEN.
/// </para>
///
/// <para>
/// <b>Idempotent</b> — kan kjøres om igjen uten å duplisere, slik alle appens øvrige seeds er (samme
/// mønster som <see cref="OrganisasjonsregisterSeed"/>/<see cref="DepartementSeed"/>): hvert lag
/// slås opp før det opprettes, og de underliggende tjenestene har selv duplikatsperrer
/// (<c>ux_gruppe_medlemskap_par</c>, <c>ux_begreper_gruppebegrep_term_lovkilde</c>,
/// <c>tekst_tagger_unik_tagg</c>). To kjøringer gir samme radantall.
/// </para>
///
/// <para>
/// <b>Ingenting oppfinnes.</b> Mangler rettskildene, nodene eller kommunene i databasen, hopper
/// seeden over det som mangler og RAPPORTERER hva som ble hoppet over i
/// <see cref="SamiskSprakforvaltningSeedResultat.HoppetOver"/> — den oppretter aldri en
/// <see cref="Virksomhet"/>, en rettskilde eller en node for å få eksempelet til å se komplett ut.
/// Det er også derfor den er trygg å kjøre i et tomt testmiljø: da gjør den ingenting.
/// </para>
/// </summary>
public static class SamiskSprakforvaltningSeed
{
    private const string SeedBruker = "Kari Jurist";

    private const string SamelovEli = "https://lovdata.no/eli/lov/1987/06/12/56/nor";
    private const string ForskriftEli = "https://lovdata.no/eli/forskrift/2005/06/17/657/nor";

    /// <summary>Sameloven § 3-1 første ledd punkt 1 — noden som DEFINERER gruppekonseptene.</summary>
    private const string DefinisjonNodeEid = SamelovEli + "/§3-1/ledd-1/punkt-1";

    /// <summary>Forskriften § 1 første ledd — noden som NAVNGIR medlemmene.</summary>
    private const string MedlemNodeEid = ForskriftEli + "/§1/ledd-1";

    /// <summary>Den overordnede gruppen. Termene er skrevet slik de faktisk står i lovteksten
    /// (små bokstaver), samme konvensjon som
    /// <see cref="NavnekandidatOppdagelseTjeneste.GodkjennAsync"/> bruker for gruppekandidater.</summary>
    private const string Forvaltningsomradet = "forvaltningsområdet for samiske språk";

    private const string Sprakutvikling = "språkutviklingskommuner";
    private const string Sprakvitalisering = "språkvitaliseringskommuner";
    private const string Sprakstimulering = "språkstimuleringskommuner";

    /// <summary>De tre kommunekategoriene, som ER medlemsgrupper av <see cref="Forvaltningsomradet"/>.</summary>
    private static readonly string[] Kategorier = [Sprakutvikling, Sprakvitalisering, Sprakstimulering];

    /// <param name="Kortform">Navnet slik det STÅR i forskriftsteksten — det er denne strengen som
    /// tagges, og den er nettopp derfor en <c>'kortform'</c>-navneform: «Karasjok» er ikke
    /// virksomhetens offisielle navn («Karasjoga gielda / karasjok kommune»).</param>
    /// <param name="Organisasjonsnummer">Oppslagsnøkkelen mot <see cref="Virksomhet"/>. Bevisst orgnr
    /// og ikke navn: de offisielle kommunenavnene er tospråklige og skrives ulikt i ulike kilder, så et
    /// navneoppslag ville vært skjørt. Orgnr er stabilt.</param>
    private sealed record Kommune(string Kortform, string Organisasjonsnummer, string Kategori);

    /// <summary>Nøyaktig de kommunene forskriften § 1 navngir, i tekstens egen rekkefølge.</summary>
    private static readonly Kommune[] Kommuner =
    [
        new("Karasjok", "963376030", Sprakutvikling),
        new("Kautokeino", "945475056", Sprakutvikling),
        new("Nesseby", "839953062", Sprakutvikling),
        new("Tana", "943505527", Sprakutvikling),

        new("Porsanger", "959411735", Sprakvitalisering),
        new("Kåfjord", "940363586", Sprakvitalisering),
        new("Lavangen", "959469881", Sprakvitalisering),
        new("Tjeldsund", "959469326", Sprakvitalisering),
        new("Hattfjelldal", "944716904", Sprakvitalisering),
        new("Hamarøy", "970542507", Sprakvitalisering),
        new("Røyrvik", "964982120", Sprakvitalisering),
        new("Røros", "939898743", Sprakvitalisering),
        new("Snåsa", "964982031", Sprakvitalisering),

        new("Saltdal", "972417734", Sprakstimulering),
    ];

    public static async Task<SamiskSprakforvaltningSeedResultat> SeedAsync(
        RegelIdeDbContext db,
        VirksomhetsbegrepTjeneste virksomhetsbegrep,
        GruppeMedlemskapTjeneste gruppeMedlemskap,
        MyndighetstildelingTjeneste myndighetstildeling,
        TekstTaggTjeneste tekstTagg,
        VirksomhetOppslagTjeneste virksomhetOppslag,
        CancellationToken ct = default)
    {
        var hoppetOver = new List<string>();

        var samelov = await FinnRettskildeAsync(db, SamelovEli, ct);
        var forskrift = await FinnRettskildeAsync(db, ForskriftEli, ct);
        if (samelov is null || forskrift is null)
        {
            // Korpuset er ikke importert i dette miljøet — ingenting å hjemle eksempelet i, og
            // ingenting skal oppfinnes. Se klassekommentaren.
            if (samelov is null) hoppetOver.Add($"Sameloven ({SamelovEli}) finnes ikke i korpuset.");
            if (forskrift is null) hoppetOver.Add($"Forskriften ({ForskriftEli}) finnes ikke i korpuset.");
            return new SamiskSprakforvaltningSeedResultat(0, 0, 0, 0, 0, hoppetOver);
        }

        var definisjonNode = await FinnNodeAsync(db, samelov.Id, DefinisjonNodeEid, ct);
        var medlemNode = await FinnNodeAsync(db, forskrift.Id, MedlemNodeEid, ct);
        if (definisjonNode?.Tekst is null || medlemNode?.Tekst is null)
        {
            if (definisjonNode?.Tekst is null) hoppetOver.Add($"Definisjonsnoden {DefinisjonNodeEid} mangler eller har ingen tekst.");
            if (medlemNode?.Tekst is null) hoppetOver.Add($"Medlemsnoden {MedlemNodeEid} mangler eller har ingen tekst.");
            return new SamiskSprakforvaltningSeedResultat(0, 0, 0, 0, 0, hoppetOver);
        }

        // Taggene må EIES av en virksomhet (TekstTaggEntitet.VirksomhetId er ikke-nullbar). Gjenbruker
        // nøyaktig samme «ansvarlig departement»-oppslag som
        // NavnekandidatOppdagelseTjeneste.OpprettDepartementTaggHvisMuligAsync — ikke en egen
        // eierskapsregel for seedet innhold.
        var samelovEier = await FinnDepartementEierAsync(samelov, virksomhetOppslag);
        var forskriftEier = await FinnDepartementEierAsync(forskrift, virksomhetOppslag);

        // ---------- 1. De fire gruppebegrepene, alle hjemlet i SAMELOVEN ----------
        var gruppebegrepPerTerm = new Dictionary<string, BegrepEntitet>(StringComparer.Ordinal);
        foreach (var term in Kategorier.Append(Forvaltningsomradet))
        {
            gruppebegrepPerTerm[term] = await SorgForGruppebegrepAsync(
                db, virksomhetsbegrep, samelov.Id, term, DefinisjonNodeEid, ct);
        }

        // ---------- 2. Tagg gruppetermene der loven DEFINERER dem ----------
        var antallTagger = 0;
        if (samelovEier is null)
        {
            hoppetOver.Add(
                "Sameloven har ingen ansvarlig departement som finnes i virksomhetskatalogen — "
                + "gruppetermene er derfor ikke tagget (en tagg må eies av en virksomhet).");
        }
        else
        {
            foreach (var (term, begrep) in gruppebegrepPerTerm)
            {
                var truffet = await SorgForTaggAsync(
                    db, tekstTagg, samelov.Id, samelovEier.Value, definisjonNode, term, "begrep", begrep.Id, ct);
                if (truffet) antallTagger++;
                else hoppetOver.Add($"Fant ikke «{term}» som eget ord i {DefinisjonNodeEid} — ingen tagg.");
            }
        }

        // ---------- 3. Gruppe av gruppe: de tre kategoriene er medlemsgrupper ----------
        var overordnet = gruppebegrepPerTerm[Forvaltningsomradet];
        var hjemmelSpenn = new[] { new ParagrafspennPar(MedlemNodeEid, null) };
        var antallMedlemskap = 0;
        foreach (var kategori in Kategorier)
        {
            await gruppeMedlemskap.OpprettAsync(
                overordnet.Id, gruppebegrepPerTerm[kategori].Id, forskrift.Id, hjemmelSpenn, SeedBruker, ct: ct);
            antallMedlemskap++;
        }

        // ---------- 4. De konkrete kommunene: navneform + tildeling + tagg ----------
        var antallTildelinger = 0;
        var antallNavneformer = 0;
        foreach (var kommune in Kommuner)
        {
            var virksomhet = await db.Virksomheter.FirstOrDefaultAsync(
                v => v.Organisasjonsnummer == kommune.Organisasjonsnummer, ct);
            if (virksomhet is null)
            {
                hoppetOver.Add(
                    $"{kommune.Kortform} (orgnr {kommune.Organisasjonsnummer}) finnes ikke som virksomhet — hoppet over.");
                continue;
            }

            // Navneformen: «Karasjok» → «Karasjoga gielda / karasjok kommune», med grunn 'kortform'.
            await SorgForNavneformAsync(db, virksomhetsbegrep, virksomhet.Id, kommune.Kortform, ct);
            antallNavneformer++;

            var gruppe = gruppebegrepPerTerm[kommune.Kategori];
            var tildeling = await db.Myndighetstildelinger.FirstOrDefaultAsync(
                m => m.GruppeBegrepId == gruppe.Id && m.VirksomhetId == virksomhet.Id
                     && m.HjemmelRettskildeId == forskrift.Id, ct);
            if (tildeling is null)
            {
                await myndighetstildeling.OpprettAsync(
                    gruppe.Id, virksomhet.Id, forskrift.Id, hjemmelSpenn, vilkaar: null, SeedBruker, ct: ct);
            }
            antallTildelinger++;

            if (forskriftEier is null) continue; // rapportert samlet under.
            var truffet = await SorgForTaggAsync(
                db, tekstTagg, forskrift.Id, forskriftEier.Value, medlemNode, kommune.Kortform,
                "virksomhet", virksomhet.Id, ct);
            if (truffet) antallTagger++;
            else hoppetOver.Add($"Fant ikke «{kommune.Kortform}» som eget ord i {MedlemNodeEid} — ingen tagg.");
        }

        if (forskriftEier is null)
        {
            hoppetOver.Add(
                "Forskriften har ingen ansvarlig departement som finnes i virksomhetskatalogen — "
                + "kommunenavnene er derfor ikke tagget (en tagg må eies av en virksomhet).");
        }

        return new SamiskSprakforvaltningSeedResultat(
            gruppebegrepPerTerm.Count, antallMedlemskap, antallTildelinger, antallNavneformer,
            antallTagger, hoppetOver);
    }

    private static Task<RettskildeEntitet?> FinnRettskildeAsync(RegelIdeDbContext db, string eli, CancellationToken ct) =>
        db.Rettskilder.FirstOrDefaultAsync(r => r.Eli == eli && r.Entitetsstatus == "gjeldende", ct);

    private static Task<RettskildeNodeEntitet?> FinnNodeAsync(
        RegelIdeDbContext db, Guid rettskildeId, string eid, CancellationToken ct) =>
        db.RettskildeNoder.FirstOrDefaultAsync(n => n.RettskildeId == rettskildeId && n.Eid == eid, ct);

    /// <summary>Første ansvarlige departement som løser til en ekte virksomhet, eller <c>null</c> —
    /// identisk regel og begrunnelse som i
    /// <c>NavnekandidatOppdagelseTjeneste.OpprettDepartementTaggHvisMuligAsync</c>.</summary>
    private static async Task<Guid?> FinnDepartementEierAsync(
        RettskildeEntitet rettskilde, VirksomhetOppslagTjeneste virksomhetOppslag)
    {
        if (rettskilde.AnsvarligDepartement is null) return null;
        foreach (var departement in rettskilde.AnsvarligDepartement)
        {
            var id = await virksomhetOppslag.FinnVirksomhetIdForNavnAsync(departement);
            if (id is not null) return id;
        }
        return null;
    }

    private static async Task<BegrepEntitet> SorgForGruppebegrepAsync(
        RegelIdeDbContext db, VirksomhetsbegrepTjeneste virksomhetsbegrep,
        Guid lovkildeId, string term, string lovreferanseEid, CancellationToken ct)
    {
        var eksisterende = await db.Begreper.FirstOrDefaultAsync(
            b => b.Begrepskategori == "gruppe" && b.LovkildeId == lovkildeId && b.Term == term
                 && b.Entitetsstatus == "gjeldende", ct);
        if (eksisterende is not null)
        {
            // Et gruppebegrep opprettet MANUELT (POST /api/gruppebegrep) har ingen LovreferanseEid, og
            // da lander «hjemlet i»-lenken på loven uten node. Fyll den inn — men bare når den mangler,
            // slik at et menneskes eget, avvikende valg aldri overskrives stille.
            if (eksisterende.LovreferanseEid is null)
            {
                eksisterende.LovreferanseEid = lovreferanseEid;
                eksisterende.SistEndretAv = SeedBruker;
                eksisterende.SistEndretTidspunkt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            return eksisterende;
        }
        return await virksomhetsbegrep.OpprettGruppebegrepAsync(lovkildeId, term, SeedBruker, lovreferanseEid, ct);
    }

    private static async Task SorgForNavneformAsync(
        RegelIdeDbContext db, VirksomhetsbegrepTjeneste virksomhetsbegrep,
        Guid virksomhetId, string term, CancellationToken ct)
    {
        var eksisterende = await db.Begreper.FirstOrDefaultAsync(
            b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == virksomhetId
                 && b.Term == term && b.Entitetsstatus == "gjeldende", ct);
        if (eksisterende is not null)
        {
            if (eksisterende.Navneformgrunn is null)
            {
                eksisterende.Navneformgrunn = "kortform";
                eksisterende.SistEndretAv = SeedBruker;
                eksisterende.SistEndretTidspunkt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            return;
        }
        await virksomhetsbegrep.OpprettVirksomhetsbegrepAsync(
            virksomhetId, term, SeedBruker, skosUrl: null, navneformgrunn: "kortform", ct);
    }

    /// <summary>
    /// Sørger for at <paramref name="term"/> er tagget som eget ord i
    /// <paramref name="node"/> og at taggen peker på <paramref name="refId"/>. Returnerer
    /// <c>false</c> når termen ikke står som eget ord i noden (ingen tagg opprettes — «ikke funnet ≠
    /// oppfunnet»).
    /// <para>
    /// Gjenbruker en eksisterende UBUNDET tagg på samme posisjon i stedet for å legge en overlappende
    /// ny ved siden av — samme regel som
    /// <c>NavnekandidatOppdagelseTjeneste.OpprettEllerKobleVirksomhetTaggAsync</c>, slik at et
    /// tidligere sveip over samme node ikke gir dobbelt merking.
    /// </para>
    /// </summary>
    private static async Task<bool> SorgForTaggAsync(
        RegelIdeDbContext db, TekstTaggTjeneste tekstTagg, Guid rettskildeId, Guid eierVirksomhetId,
        RettskildeNodeEntitet node, string term, string kind, Guid refId, CancellationToken ct)
    {
        var tekst = node.Tekst!;
        var start = FinnHeltOrd(tekst, term);
        if (start < 0) return false;
        var slutt = start + term.Length;

        var paaPosisjonen = await db.TekstTagger
            .Where(t => t.RettskildeId == rettskildeId && t.NodeEid == node.Eid
                        && t.StartOffset == start && t.EndOffset == slutt
                        && t.Entitetsstatus == "gjeldende")
            .ToListAsync(ct);

        if (paaPosisjonen.Any(t => t.Kind == kind && t.RefId == refId)) return true; // alt på plass.

        var ubundet = paaPosisjonen.FirstOrDefault(t => t.RefId is null);
        if (ubundet is not null)
        {
            ubundet.Kind = kind;
            ubundet.RefId = refId;
            db.Proveniens.Add(ProveniensHjelper.NyRad(
                "tekst_tagg", ubundet.Id, ubundet.VirksomhetId, "endret", SeedBruker));
            await db.SaveChangesAsync(ct);
            return true;
        }

        const int kontekstLengde = 30; // samme vindu som resten av kodebasen bruker.
        var tagg = await tekstTagg.OpprettAsync(
            rettskildeId, eierVirksomhetId, SeedBruker, node.Eid, start, slutt,
            tekst[Math.Max(0, start - kontekstLengde)..start],
            tekst[start..slutt],
            tekst[slutt..Math.Min(tekst.Length, slutt + kontekstLengde)],
            kind, ct);
        if (tagg is null) return false;
        await tekstTagg.KobleTilEntitetAsync(tagg.Id, refId, SeedBruker, ct);
        return true;
    }

    /// <summary>
    /// Første forekomst av <paramref name="term"/> i <paramref name="tekst"/> som står som EGET ORD
    /// (ikke inne i et lengre ord), eller <c>-1</c>. Ordgrensesjekken er nødvendig fordi flere av
    /// termene er prefikser av hverandre i denne teksten: «språkutviklingskommuner» ville ellers også
    /// truffet inne i lengre sammensetninger, og en kommune-kortform ville kunnet treffe midt i et
    /// annet navn.
    /// </summary>
    private static int FinnHeltOrd(string tekst, string term)
    {
        var fra = 0;
        while (true)
        {
            var treff = tekst.IndexOf(term, fra, StringComparison.Ordinal);
            if (treff < 0) return -1;
            var foreOk = treff == 0 || !char.IsLetter(tekst[treff - 1]);
            var slutt = treff + term.Length;
            var etterOk = slutt >= tekst.Length || !char.IsLetter(tekst[slutt]);
            if (foreOk && etterOk) return treff;
            fra = treff + 1;
        }
    }
}

/// <summary>
/// Hva <see cref="SamiskSprakforvaltningSeed.SeedAsync"/> sørget for at finnes (ikke hvor mange rader
/// som ble opprettet NETT denne kjøringen — etter en idempotent gjentakelse er tallene like, mens
/// antall NYE rader ville vært 0. Det er «finnes»-tallet som er interessant både i loggen ved oppstart
/// og i idempotens-testen).
/// </summary>
/// <param name="HoppetOver">Alt som IKKE kunne seedes, med begrunnelse — tom liste betyr komplett
/// eksempel. Logges ved oppstart slik at et halvt seedet miljø er synlig, ikke stille.</param>
public sealed record SamiskSprakforvaltningSeedResultat(
    int AntallGruppebegrep, int AntallGruppemedlemskap, int AntallMyndighetstildelinger,
    int AntallNavneformer, int AntallTagger, IReadOnlyList<string> HoppetOver);
