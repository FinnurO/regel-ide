using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Testcase-innhold for byggesteg 4 runde 1 (Vilkårstre, docs/06-veikart.md) — bygger nøyaktig treet
/// fra docs/01-referansemodell.md §5.5 (det låste, syv-invariant-testede alkoholloven-eksempelet), og
/// kobler det som rotnode for "Alminnelig skjenkebevilling" (byggesteg 2). Samme idempotens-mønster
/// som <see cref="Byggesteg2InnholdSeed"/> — global guard, kjøres etter den i Program.cs.
///
/// Ingen formel-eksempel seedes her (bevillingsgebyret nevnt i docs/10-rules-as-code-landskap.md er
/// ikke del av det låste §5.5-testcaset) — <see cref="VilkarEntitet.ErFormel"/> er tilgjengelig, men
/// bevisst ubrukt i disse seed-dataene.
///
/// 2026-07-30: seeder også ekte <c>kind='vilkar'</c>-tekst-tagger på de relevante paragrafene, koblet
/// til sitt Vilkår via <see cref="TekstTaggTjeneste.KobleTilEntitetAsync"/> — uten dette var koblingen
/// vilkårstre→lovtekst kun teoretisk mulig, aldri faktisk demonstrert (se brukertilbakemelding
/// 2026-07-30: "Vilkår i vilkårstreet som ikke er knyttet til [tekst-tagger i] Vilkår").
/// </summary>
public static class Byggesteg4VilkarstreSeed
{
    private const string SeedBruker = "Kari Jurist";
    private const string AlkohollovenEli = "https://lovdata.no/eli/lov/1989/06/02/27/nor";
    private const string RotnodeTittel = "Vedtak om skjenkebevilling";

    public static async Task SeedAsync(RegelIdeDbContext db, CancellationToken ct = default)
    {
        if (await db.Regelnoder.AnyAsync(r => r.Tittel == RotnodeTittel, ct)) return; // global guard, se Byggesteg2InnholdSeed

        var testkommunen = await db.Virksomheter.FirstOrDefaultAsync(v => v.Navn == "Testkommunen", ct);
        if (testkommunen is null) return;

        var tjeneste = await db.Tjenester.FirstOrDefaultAsync(t => t.Tittel == "Alminnelig skjenkebevilling", ct);
        var vandelBegrep = await db.Begreper.FirstOrDefaultAsync(b => b.Term == "uklanderlig vandel", ct);
        if (tjeneste is null || vandelBegrep is null) return; // byggesteg 2-seedingen må ha kjørt først

        var alkoholloven = await db.Rettskilder.FirstOrDefaultAsync(r => r.Eli == AlkohollovenEli && r.Entitetsstatus == "gjeldende", ct);

        var virksomhetId = testkommunen.Id;

        var styrerFodselsdato = await NyttDatasettAsync(db, virksomhetId,
            "Styrers fødselsdato", "styrer.fodselsdato", "date", "brukeroppgitt", ct);
        var lokaletype = await NyttDatasettAsync(db, virksomhetId,
            "Skjenkestedets lokaletype", "skjenkested.lokaletype", "string", "brukeroppgitt", ct);
        var klokkeslett = await NyttDatasettAsync(db, virksomhetId,
            "Tidspunkt for skjenking", "klokkeslett.tidspunkt", "string", "oppslagbart", ct);
        var erLukketSelskap = await NyttDatasettAsync(db, virksomhetId,
            "Er arrangementet et lukket selskap", "arrangement.er_lukket_selskap", "boolean", "brukeroppgitt", ct);

        var vilkarregister = new VilkarregisterTjeneste(db);
        var regelnoderegister = new RegelnoderegisterTjeneste(db);
        var unntaksregister = new UnntaksregisterTjeneste(db);
        var tjenesteregister = new TjenesteregisterTjeneste(db);
        var tekstTaggTjeneste = new TekstTaggTjeneste(db);

        var vAlder = await vilkarregister.OpprettAsync(
            virksomhetId, "Aldersvilkår", "Styrer og stedfortreder må være over 20 år.", null, "materiell", "styrer/stedfortreder",
            [new JuridiskGrunnlagInput("alkoholloven", $"{AlkohollovenEli}/§1-5")], null, "regelbasert", """{"minimumsalder":20}""",
            null, null, false, null, null, null, false, null, SeedBruker, ct);
        await vilkarregister.LeggTilInputAsync(vAlder.Id, styrerFodselsdato.Id, ct);
        await SeedVilkarTaggAsync(db, tekstTaggTjeneste, alkoholloven, virksomhetId, "§1-5", vAlder.Id, ct);

        var vVandel = await vilkarregister.OpprettAsync(
            virksomhetId, "Vandelsvilkår", "Bevillingshaver og personer med vesentlig innflytelse må ha utvist uklanderlig vandel.",
            null, "materiell", "bevillingshaver", [new JuridiskGrunnlagInput("alkoholloven", $"{AlkohollovenEli}/§1-7b")],
            vandelBegrep.Id, "skjonnsbasert", null, vandelBegrep.Id,
            [
                new SkjonnsmomentInput("Tidligere bevillingsbrudd", null, null),
                new SkjonnsmomentInput("Økonomisk vandel", null, null),
                new SkjonnsmomentInput("Straffbare forhold", null, null),
            ],
            true, "Jurist", null, null, false, null, SeedBruker, ct);
        await SeedVilkarTaggAsync(db, tekstTaggTjeneste, alkoholloven, virksomhetId, "§1-7b", vVandel.Id, ct);

        var vSted = await vilkarregister.OpprettAsync(
            virksomhetId, "Stedsvilkår", "Skjenkestedet må være et lokale det kan gis bevilling til.", null, "formell", null,
            [new JuridiskGrunnlagInput("alkoholloven", $"{AlkohollovenEli}/§4-3")], null, "regelbasert", null,
            null, null, false, null, null, null, false, null, SeedBruker, ct);
        await vilkarregister.LeggTilInputAsync(vSted.Id, lokaletype.Id, ct);
        await SeedVilkarTaggAsync(db, tekstTaggTjeneste, alkoholloven, virksomhetId, "§4-3", vSted.Id, ct);

        var vKlokkeslett = await vilkarregister.OpprettAsync(
            virksomhetId, "Klokkeslettsvilkår", "Skjenkingen må skje innenfor kommunens fastsatte skjenketid.", null, "formell", null,
            [new JuridiskGrunnlagInput("alkoholloven", $"{AlkohollovenEli}/§4-4")], null, "regelbasert", null,
            null, null, false, null, null, null, false, null, SeedBruker, ct);
        await vilkarregister.LeggTilInputAsync(vKlokkeslett.Id, klokkeslett.Id, ct);
        await SeedVilkarTaggAsync(db, tekstTaggTjeneste, alkoholloven, virksomhetId, "§4-4", vKlokkeslett.Id, ct);

        var vErLukketSelskap = await vilkarregister.OpprettAsync(
            virksomhetId, "Er lukket selskap", "Hvorvidt arrangementet er et lukket selskap (unntak fra skjenketid).", null, "formell", null,
            [new JuridiskGrunnlagInput("alkoholloven", $"{AlkohollovenEli}/§4-4")], null, "regelbasert", null,
            null, null, false, null, null, null, false, null, SeedBruker, ct);
        await vilkarregister.LeggTilInputAsync(vErLukketSelskap.Id, erLukketSelskap.Id, ct);

        var rSkjenketid = await regelnoderegister.OpprettAsync(
            virksomhetId, "Skjenketid oppfylt", "Skjenking skjer på riktig sted og innenfor riktig tidsrom.", null, "OG",
            "Skjenketid oppfylt", "boolean", false, null, null, null, SeedBruker, ct);
        await regelnoderegister.KobleBarnAsync(rSkjenketid.Id, "vilkar", vSted.Id, ct);
        await regelnoderegister.KobleBarnAsync(rSkjenketid.Id, "vilkar", vKlokkeslett.Id, ct);

        await unntaksregister.OpprettAsync(
            virksomhetId, "Unntak for lukket selskap", "Skjenketidsbegrensningen gjelder ikke for lukket selskap.",
            rSkjenketid.Id, "vilkar", vErLukketSelskap.Id, null, SeedBruker, ct);

        var rRoot = await regelnoderegister.OpprettAsync(
            virksomhetId, RotnodeTittel, "Rotnoden for tjenesten «Alminnelig skjenkebevilling».", null, "OG",
            "Vedtak om skjenkebevilling", "vedtak", true, null, null, null, SeedBruker, ct);
        await regelnoderegister.KobleBarnAsync(rRoot.Id, "vilkar", vAlder.Id, ct);
        await regelnoderegister.KobleBarnAsync(rRoot.Id, "vilkar", vVandel.Id, ct);
        await regelnoderegister.KobleBarnAsync(rRoot.Id, "regelnode", rSkjenketid.Id, ct);

        await tjenesteregister.SettRotnodeAsync(tjeneste.Id, rRoot.Id, ct);
    }

    /// <summary>
    /// Tagger første ledd i en gitt paragraf som <c>kind='vilkar'</c> og kobler taggen umiddelbart til
    /// det tilhørende Vilkåret — selve fiksen på "Vilkår i vilkårstreet uten sporbar kobling tilbake
    /// til lovteksten" (brukertilbakemelding 2026-07-30). No-op hvis <paramref name="alkoholloven"/> er
    /// null (alkoholloven ikke importert) eller ledd-noden ikke finnes.
    /// </summary>
    private static async Task SeedVilkarTaggAsync(
        RegelIdeDbContext db, TekstTaggTjeneste tekstTaggTjeneste, RettskildeEntitet? alkoholloven,
        Guid virksomhetId, string paragrafnummer, Guid vilkarId, CancellationToken ct)
    {
        if (alkoholloven is null) return;

        var leddEid = $"{AlkohollovenEli}/{paragrafnummer}/ledd-1";
        var node = await db.RettskildeNoder.FirstOrDefaultAsync(
            n => n.RettskildeId == alkoholloven.Id && n.Eid == leddEid && n.Tekst != null, ct);
        if (node?.Tekst is null) return;

        var lengde = Math.Min(40, node.Tekst.Length);
        var tagg = await tekstTaggTjeneste.OpprettAsync(
            alkoholloven.Id, virksomhetId, SeedBruker, node.Eid, 0, lengde,
            "", node.Tekst[..lengde], node.Tekst[lengde..], "vilkar", ct);
        if (tagg is not null)
        {
            await tekstTaggTjeneste.KobleTilEntitetAsync(tagg.Id, vilkarId, SeedBruker, ct);
        }
    }

    private static async Task<DatasettEntitet> NyttDatasettAsync(
        RegelIdeDbContext db, Guid virksomhetId, string felt, string prop, string dtype, string type, CancellationToken ct)
    {
        var eksisterende = await db.Datasett.FirstOrDefaultAsync(d => d.Prop == prop, ct);
        if (eksisterende is not null) return eksisterende;

        var datasett = new DatasettEntitet
        {
            Id = Guid.NewGuid(), VirksomhetId = virksomhetId, Felt = felt, Prop = prop, Dtype = dtype, Type = type,
            OpprettetAv = SeedBruker, OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Datasett.Add(datasett);
        await db.SaveChangesAsync(ct);
        return datasett;
    }
}
