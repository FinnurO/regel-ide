namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #429 «Funn til vurdering», Johanns avgjørelser 2026-10-29/30] ÉN kilde for de lukkede vokabularene til
/// <see cref="FunnEntitet"/>, <see cref="FunnKoblingEntitet"/> og <see cref="FunnVurderingspunktEntitet"/> — speilet av
/// CHECK-ene i <see cref="RegelIdeDbContext"/>.
/// <para>
/// <b>Hva et funn er:</b> et sted der rettskildene ikke stemmer med seg selv eller med virkeligheten — en observasjon fra
/// modellen med kilder, <i>ikke</i> en påstand om feil i loven. Ordbruken er låst: «funn til vurdering», aldri «feil» eller
/// «avvik» (#429 AK5). Kildefeil (<see cref="KildefeilEntitet"/>, #249) er noe annet: en teknisk feil i Lovdata-kopien som vi
/// følger opp internt. Et funn kan peke på en kildefeil (<see cref="FunnKoblingEntitet.KildefeilId"/>).
/// </para>
/// <para>
/// <b>Statusflyten</b> (#429): <c>oppdaget</c> → <c>juristvurdert</c> → <c>godkjent_for_publisering</c>, eller
/// <c>forklart_bort</c>. Bare de publiseringsansvarlige (<see cref="KonfignokkelPubliseringsansvarlige"/>, i praksis Johann)
/// kan godkjenne for publisering eller trekke tilbake; det sjekkes i API-et. Funn som er forklart bort, publiseres ikke
/// (Johann 2026-10-30, anbefaling 8).
/// </para>
/// </summary>
public static class Funn
{
    public const string ProveniensType = "funn";

    // ---- Kategori og undertype (#429, Johann 2026-10-29) ----
    public const string UtgattAktor = "utgatt_aktor";
    public const string MuligMotstrid = "mulig_motstrid";
    public const string UklarhetHull = "uklarhet_hull";
    public static readonly string[] Kategorier = [UtgattAktor, MuligMotstrid, UklarhetHull];

    /// <summary>Undertypene per kategori. <c>mulig_motstrid</c> har ingen. NULL er alltid lov (ikke angitt).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Undertyper = new Dictionary<string, string[]>
    {
        [UtgattAktor] = ["nedlagt_aktor", "utgatt_navn"],
        [MuligMotstrid] = [],
        [UklarhetHull] = ["hull", "upresis_henvisning", "publiseringsfeil_kilde"],
    };

    // ---- Status ----
    public const string Oppdaget = "oppdaget";
    public const string Juristvurdert = "juristvurdert";
    public const string GodkjentForPublisering = "godkjent_for_publisering";
    public const string ForklartBort = "forklart_bort";
    public static readonly string[] Statuser = [Oppdaget, Juristvurdert, GodkjentForPublisering, ForklartBort];

    /// <summary>Hvem som juristvurderte: et menneske, eller juristagenten (<c>.claude/agents/jurist-utfordrer.md</c>, CLAUDE.md §23).
    /// KI-vurderingen teller som juristvurdert, men vises med eget merke (Johann 2026-10-30, anbefaling 7; docs/09 §46).</summary>
    public const string Menneske = "menneske";
    public const string KiAgent = "ki_agent";
    public static readonly string[] VurdertAvTyper = [Menneske, KiAgent];

    // ---- Koblingsroller ----
    public const string GjelderBestemmelse = "gjelder_bestemmelse";
    public const string MotstridendeBestemmelse = "motstridende_bestemmelse";
    public const string BerortAktor = "berort_aktor";
    public const string Navneform = "navneform";
    public const string Etterfolger = "etterfolger";
    public const string Kilde = "kilde";
    /// <summary>[Ny, #429 PR 2, Johanns avgjørelse 2026-10-31 (2)] Vedtaket et endringsvedtak endrer: «funn i endringsvedtak merkes med
    /// målvedtaket». Settes bare når det kan finnes uten gjetting — rettskilden endrer nøyaktig ETT dokument ifølge Lovdatas «Endrer»-felt
    /// (<see cref="RettskildeEndringEntitet"/>). Publiseres ikke som egen liste; påstanden sier det i klartekst.</summary>
    public const string Malvedtak = "malvedtak";
    public static readonly string[] Koblingsroller =
        [GjelderBestemmelse, MotstridendeBestemmelse, BerortAktor, Navneform, Etterfolger, Kilde, Malvedtak];

    /// <summary>[Ny, #429 PR 2, Johanns avgjørelse 2026-10-31 (3): premissporten for etterfølger gjelder alle fire gruppene] Overskriften
    /// på vurderingspunktet (type <see cref="Vurdering"/>) der juristen sier hvem som har oppgaven nå, eller at det er ukjent, når funnet
    /// ikke peker på noen etterfølgerkant. Uten et slikt punkt stopper premissporten («Etterfølger ikke vurdert»). Se
    /// <see cref="FunnTjeneste.PremissmanglerAsync"/>.</summary>
    public const string EtterfolgerOverskrift = "Etterfølger";

    // ---- Vurderingspunkter ----
    public const string BortforklaringProvd = "bortforklaring_provd";
    public const string Vurdering = "vurdering";
    public const string IkkeAvgjort = "ikke_avgjort";
    public static readonly string[] VurderingspunktTyper = [BortforklaringProvd, Vurdering, IkkeAvgjort];

    /// <summary>Sikkerheten slik juristvurderingene i #429 uttrykker den («høy», «middels til høy»). Bare på type <c>vurdering</c>.</summary>
    public static readonly string[] Sikkerheter = ["hoy", "middels_til_hoy", "middels", "lav_til_middels", "lav"];

    // ---- Konfigurasjon ----
    /// <summary>[LÅST, Johann 2026-10-30, #429 anbefaling 6] Bruker-id-ene (Guid fra <c>brukere.id</c>) som kan godkjenne for
    /// publisering og trekke tilbake. <b>Standardverdien er tom: da kan ingen publisere.</b> Testbrukermodus (X-Bruker-Id) er ikke
    /// sikkerhet — hvem som helst kan sende hvilken som helst id. Den reelle sperren er at nettsiden bare oppdateres når en PR med
    /// <c>nettside/data/funn/register.json</c> flettes til master.</summary>
    public const string KonfignokkelPubliseringsansvarlige = "RegelIde:Funn:Publiseringsansvarlige";

    /// <summary>Seed av de fem funnene fra #429 ved oppstart. Skriver til databasen ⇒ gated (CLAUDE.md §4), false i
    /// <c>EmbeddedPostgresApiFixture</c>.</summary>
    public const string KonfignokkelSeedVedOppstart = "RegelIde:Funn:SeedVedOppstart";
}

/// <summary>[Ny, issue #429] Ett funn til vurdering. Se <see cref="Funn"/> for vokabularene og statusflyten.
/// <para>
/// <b>Det publiserte innholdet er et øyeblikksbilde.</b> Ved godkjenning for publisering fryses den offentlige representasjonen i
/// <see cref="GodkjentInnholdJson"/> med SHA-256 i <see cref="GodkjentInnholdHash"/>. Eksporten til nettsiden leser BARE
/// øyeblikksbildet, aldri de levende feltene. Endres teksten, koblingene eller vurderingspunktene etterpå, går funnet tilbake til
/// <c>juristvurdert</c> og øyeblikksbildet nulles (<see cref="FunnTjeneste.OppdaterAsync"/>) — ingenting publiseres som ikke er
/// godkjent (#429 AK3).
/// </para></summary>
public sealed class FunnEntitet
{
    public Guid Id { get; set; }

    /// <summary>Stabil nøkkel for funn som er lastet fra en fil eller en agent (for eksempel <c>429-funn-1</c>). Unik når satt;
    /// idempotensnøkkelen for seed og agentregistrering. Brukes også som offentlig id på nettsiden, fordi <see cref="Id"/> er
    /// forskjellig i hvert miljø.</summary>
    public string? EksternNokkel { get; set; }

    public required string Kategori { get; set; }
    public string? Undertype { get; set; }
    public required string Tittel { get; set; }

    /// <summary>Påstanden: hva modellen observerte. Publiseres. Linjer som begynner med «- » er punkter.</summary>
    public required string Beskrivelse { get; set; }

    /// <summary>Juristens samlede vurdering. Publiseres sammen med vurderingspunktene.</summary>
    public string? Vurdering { get; set; }

    /// <summary>Intern merknad. Publiseres aldri.</summary>
    public string? InternMerknad { get; set; }

    public string Status { get; set; } = Funn.Oppdaget;

    /// <summary><c>manuell</c> | <c>monster:&lt;id&gt;</c> | <c>ki:&lt;modell&gt;</c> | <c>agent:jurist-utfordrer</c> | <c>seed:&lt;fil&gt;</c>.</summary>
    public required string OppdagelsesKilde { get; set; }

    /// <summary>Saken funnet kom fram i, for eksempel «#375». Fritekst.</summary>
    public string? FunnetI { get; set; }

    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
    public string? SistEndretAv { get; set; }
    public DateTimeOffset? SistEndretTidspunkt { get; set; }

    public string? JuristvurdertAv { get; set; }
    public DateTimeOffset? JuristvurdertTidspunkt { get; set; }
    /// <summary><see cref="Funn.Menneske"/> | <see cref="Funn.KiAgent"/>.</summary>
    public string? VurdertAvType { get; set; }

    public string? ForklartBortBegrunnelse { get; set; }
    public string? ForklartBortAv { get; set; }
    public DateTimeOffset? ForklartBortTidspunkt { get; set; }

    public string? GodkjentAv { get; set; }
    public DateTimeOffset? GodkjentTidspunkt { get; set; }
    public string? GodkjentInnholdJson { get; set; }
    public string? GodkjentInnholdHash { get; set; }

    /// <summary>Satt av Lovdata-resynken når en bestemmelse funnet peker på (<see cref="FunnKoblingEntitet.NodeId"/>) har fått ny
    /// tekst (<see cref="HjemmelEndretFlagg"/>). Et flagg, ikke en statusendring: et publisert funn blir stående til Johann trekker
    /// det tilbake. Nulles ved ny juristvurdering.</summary>
    public DateTimeOffset? KildeEndretTidspunkt { get; set; }

    public List<FunnKoblingEntitet> Koblinger { get; set; } = [];
    public List<FunnVurderingspunktEntitet> Vurderingspunkter { get; set; } = [];
}

/// <summary>[Ny, issue #429] Én peker fra et funn: en bestemmelse, en aktør, en navneform, en etterfølgerkant, en kildefeil eller
/// en kilde utenfor korpus. Nøyaktig ett mål per rad (CHECK <c>ck_funn_koblinger_ett_mal</c>).
/// <para>
/// <b>Bestemmelser forankres som taggene</b>: ELI-en er den stabile nøkkelen (lik i alle miljøer), og <see cref="RettskildeId"/> og
/// <see cref="NodeId"/> slås opp mot den gjeldende versjonen når koblingen lagres. Finnes de ikke i korpuset, blir de NULL og vises
/// som «ikke i korpuset» — aldri nærmeste treff (CLAUDE.md §8).
/// </para></summary>
public sealed class FunnKoblingEntitet
{
    public Guid Id { get; set; }
    public Guid FunnId { get; set; }
    public required string Rolle { get; set; }
    public int Sortering { get; set; }

    /// <summary>Visningsteksten, slik den står i vurderingen («FOR-2008-09-12-1071 § 11 tredje ledd»). Påkrevd: den er det som
    /// publiseres, ikke registernavnet (CLAUDE.md §0, «loven definerer, registeret beskriver»).</summary>
    public required string Etikett { get; set; }

    /// <summary>Hva bestemmelsen sier i denne sammenhengen («sier Kunnskapsdepartementet»). Publiseres.</summary>
    public string? Merknad { get; set; }

    public string? RettskildeEli { get; set; }
    public Guid? RettskildeId { get; set; }
    public string? Eid { get; set; }
    public Guid? NodeId { get; set; }
    public Guid? VirksomhetId { get; set; }
    public Guid? BegrepId { get; set; }
    public Guid? StrukturkantId { get; set; }
    public Guid? KildefeilId { get; set; }
    public string? EksternLenke { get; set; }

    /// <summary>[Ny, #429 PR 2] Ordet slik det står i noden (<c>quote_exact</c>, som på taggene), for en bestemmelse funnet av
    /// maskinregelen. Forekomsten forankres dermed som en tagg: ELI + eId + det ordrette sitatet. Bare på bestemmelser med eId.</summary>
    public string? QuoteExact { get; set; }
}

/// <summary>[Ny, issue #429] Ett punkt i vurderingen: en bortforklaring som er prøvd (med resultatet), en delvurdering med
/// sikkerhet, eller noe som ikke er avgjort. Gjør «hvilke bortforklaringer er prøvd?» spørrbart (#429 AK1).</summary>
public sealed class FunnVurderingspunktEntitet
{
    public Guid Id { get; set; }
    public Guid FunnId { get; set; }
    public required string Type { get; set; }
    public int Sortering { get; set; }
    /// <summary>Kort overskrift («Opphevelse eller endring»). Valgfri.</summary>
    public string? Overskrift { get; set; }
    public required string Tekst { get; set; }
    /// <summary>Bare på type <see cref="Funn.Vurdering"/>, se <see cref="Funn.Sikkerheter"/>.</summary>
    public string? Sikkerhet { get; set; }
}
