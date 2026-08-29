using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Oppdagelsesmekanismen (docs/13-backlog.md §9) — ren tekstanalyse (regex), ALDRI KI/LLM (eksplisitt
/// instruks fra Johann, ikke bare en foretrukket løsning). Komplementær til
/// <see cref="VirksomhetKandidatSveipTjeneste"/>, som er en BEKREFTELSES-mekanisme (krever en allerede
/// kjent navneform-<see cref="BegrepEntitet"/>-rad og leter etter FLERE forekomster av DEN kjente
/// strengen). Denne klassen foreslår derimot HELT NYE kandidatnavn — mønstre, ikke kjente strenger —
/// til <see cref="NavnekandidatEntitet"/>-køen (se den klassens kommentar for hvorfor en egen tabell).
/// <para>
/// Én klasse dekker både selve sveipet og selve køen (motsatt av virksomhet-kandidat-parets to klasser)
/// — denne køen har ingen separat gjenbruker av bare "opprett/lister"-delen (VirksomhetKandidatTjeneste
/// brukes bl.a. direkte av godkjennings-flyten i TekstTaggTjeneste-sammenheng), så en splitt ga ikke
/// samme gevinst her.
/// </para>
/// <para>
/// <b>Mønstre (docs/13-backlog.md §9, Johanns liste, ikke uttømmende):</b>
/// </para>
/// <list type="number">
/// <item>Suffiksmønster + STOR forbokstav MIDT i en setning → <c>"virksomhet"</c> (ekte egennavn, f.eks.
/// "Miljødirektoratet", "Datatilsynet"). "Midt i en setning" — ikke bare fordi ordet står først i en
/// setning, se <see cref="ErSetningsstart"/> — er den avgjørende presisjonssiden: uten dette filteret
/// ville et vanlig substantiv som tilfeldigvis er stort fordi det åpner en setning (f.eks.
/// "Departementet kan …" i begynnelsen av et ledd) gitt et falskt "virksomhet"-treff.</item>
/// <item>Suffiksmønster + LITEN forbokstav → <c>"rolle"</c> (beskrivelse av en funksjon, f.eks.
/// "forurensningsmyndighetene", ikke et egennavn) — posisjon i setningen er irrelevant her, siden
/// liten forbokstav i seg selv allerede utelukker et egennavn.</item>
/// <item>Fast liste juridisk-aktør-substantiv UTEN suffiks ("Kongen", "Kongen i statsråd", "Stortinget",
/// "Regjeringen", "statsforvalteren", "kommunen", "fylkeskommunen", "departementet") → ALLTID
/// <c>"rolle"</c>, uansett store/små bokstaver — disse er generiske rollesubstantiv, ikke navn på
/// én bestemt institusjon, og posisjon i setningen endrer ikke det.</item>
/// </list>
/// <para>
/// <b>Kjøres KUN mot allerede importerte rettskilde-noder</b> — samme datakilde som
/// <see cref="VirksomhetKandidatSveipTjeneste"/> (<c>Entitetsstatus == "gjeldende" &amp;&amp; !Opphevet</c>),
/// IKKE en ny, live skraping av Lovdata. Dekningen er derfor begrenset til det som faktisk ER importert
/// — en reell begrensning, ikke noe denne klassen later som er komplett. I motsetning til
/// virksomhet-kandidat-sveipet er dette IKKE scopet til delte+én virksomhets egne rettskilder (det
/// sveipet er PER VIRKSOMHET; dette er et generelt, virksomhetsuavhengig søk etter UKJENTE navn på
/// tvers av HELE korpuset) — <paramref name="rettskildeId"/> i <see cref="SveipAsync"/> er kun en
/// valgfri innsnevring til én rettskilde, ikke et virksomhet-eierskaps-filter.
/// </para>
/// <para>
/// <b>Filtrering av allerede DEKKEDE treff</b> (docs/13-backlog.md §9): et treff som samsvarer
/// case-insensitivt med <see cref="BegrepEntitet.Term"/> til en eksisterende, gjeldende
/// <see cref="BegrepEntitet"/>-rad skal IKKE gi en ny kandidat — poenget er å oppdage NYE navn, ikke
/// duplisere det <see cref="VirksomhetKandidatSveipTjeneste"/> allerede finner/kan finne. Scopet ulikt
/// per kategori, siden identiteten er ulik (docs/20 §2.3 vs. §2.4): et <c>"virksomhet"</c>-treff sjekkes
/// mot ALLE eksisterende virksomhet-navneformer (globalt delt, uansett rettskilde) — et <c>"rolle"</c>-treff
/// sjekkes kun mot rollebegrep for NØYAKTIG DENNE rettskilden (rollebegrepets identitet er
/// <c>(Term, LovkildeId)</c> sammen, samme rollenavn i en annen lov er en annen rad og dekker ikke dette
/// treffet).
/// </para>
/// </summary>
public sealed class NavnekandidatOppdagelseTjeneste(RegelIdeDbContext db, VirksomhetsbegrepTjeneste virksomhetsbegrep)
{
    /// <summary>Suffiksene fra Johanns liste (docs/13-backlog.md §9) — sortert lengst-først i den
    /// sammensatte alternasjonen (samme "unngå kortere delvis treff av en lengre streng"-prinsipp som
    /// <see cref="VirksomhetKandidatSveipTjeneste"/>), selv om ingen av dagens suffikser er substrenger
    /// av hverandre — defensivt, ikke bevist nødvendig for akkurat denne listen.</summary>
    private static readonly string[] Suffikser =
    [
        "tilsynet", "direktoratet", "departementet", "nemnda", "nemnden",
        "domstolen", "ombudet", "verket", "etaten", "banken",
    ];

    /// <summary>Faste juridisk-aktør-substantiv UTEN suffiks (docs/13-backlog.md §9) — ALLTID
    /// <c>"rolle"</c>-kandidater, uansett store/små bokstaver. Lengst-først i alternasjonen, slik at
    /// "Kongen i statsråd" foretrekkes framfor et delvis treff på bare "Kongen".</summary>
    private static readonly string[] FasteRollesubstantiv =
    [
        "Kongen i statsråd", "Kongen", "Stortinget", "Regjeringen",
        "statsforvalteren", "kommunen", "fylkeskommunen", "departementet",
    ];

    private static readonly Regex SuffiksMønster = new(
        @"\b\p{L}[\p{L}]*(?:" + string.Join('|', Suffikser) + @")\b");

    private static readonly Regex FasteRollerMønster = new(
        @"\b(?:" + string.Join('|', FasteRollesubstantiv.OrderByDescending(s => s.Length).Select(Regex.Escape)) + @")\b",
        RegexOptions.IgnoreCase);

    /// <summary>
    /// Kjører oppdagelsessveipet — enten mot ÉN rettskilde (<paramref name="rettskildeId"/> satt) eller
    /// mot HELE det importerte korpuset (<paramref name="rettskildeId"/> = <c>null</c>).
    /// </summary>
    public async Task<NavnekandidatSveipResultat> SveipAsync(Guid? rettskildeId, string opprettetAv, CancellationToken ct = default)
    {
        if (rettskildeId is not null && !await db.Rettskilder.AnyAsync(r => r.Id == rettskildeId, ct))
        {
            throw new ArgumentException($"Fant ingen rettskilde med id '{rettskildeId}'. Ingen gjettet fallback.");
        }

        var noder = await db.RettskildeNoder
            .Join(db.Rettskilder, n => n.RettskildeId, r => r.Id, (n, r) => n)
            .Where(n => n.Tekst != null && !n.Opphevet && n.Entitetsstatus == "gjeldende"
                        && (rettskildeId == null || n.RettskildeId == rettskildeId))
            .Select(n => new { n.RettskildeId, n.Eid, n.Tekst })
            .ToListAsync(ct);

        // Eksisterende Begrep-termer, forhåndslastet ÉN gang for hele sveipet (ikke ett spørring per
        // treff) — samme "unngå N+1" -hensyn som ellers i kodebasen. To separate mengder, se
        // klassekommentaren for HVORFOR scopingen er ulik per kategori.
        var virksomhetTermer = new HashSet<string>(
            await db.Begreper.Where(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende")
                .Select(b => b.Term).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);
        var rolleTermerPerLovkilde = (await db.Begreper
                .Where(b => b.Begrepskategori == "rolle" && b.Entitetsstatus == "gjeldende" && b.LovkildeId != null)
                .Select(b => new { b.Term, b.LovkildeId }).ToListAsync(ct))
            .GroupBy(b => b.LovkildeId!.Value)
            .ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(x => x.Term), StringComparer.OrdinalIgnoreCase));

        var antallTreff = 0;
        var antallNyeKandidater = 0;
        foreach (var node in noder)
        {
            foreach (var (start, lengde, kategori) in FinnKandidaterITekst(node.Tekst!))
            {
                var tekst = node.Tekst![start..(start + lengde)];
                var alleredeDekket = kategori == "virksomhet"
                    ? virksomhetTermer.Contains(tekst)
                    : rolleTermerPerLovkilde.TryGetValue(node.RettskildeId, out var rolleTermer) && rolleTermer.Contains(tekst);
                if (alleredeDekket) continue;

                antallTreff++;
                var forAntall = await db.Navnekandidater.CountAsync(
                    k => k.RettskildeId == node.RettskildeId && k.NodeEid == node.Eid && k.StartOffset == start, ct);
                await OpprettEllerFinnAsync(tekst, kategori, node.RettskildeId, node.Eid, start, start + lengde, opprettetAv, ct);
                if (forAntall == 0) antallNyeKandidater++;
            }
        }

        return new NavnekandidatSveipResultat(antallTreff, antallNyeKandidater);
    }

    /// <summary>
    /// Ren, testbar funksjon uten DB-avhengighet — selve mønstergjenkjenningen (docs/13-backlog.md §9),
    /// separert fra sveipets DB-orkestrering slik at klassifiseringslogikken kan enhetstestes direkte
    /// mot en tekststreng, uten en hel rettskilde-node/embedded Postgres.
    /// <para>
    /// <b>"Midt i en setning"</b> (<see cref="ErSetningsstart"/>): et suffikstreff med STOR forbokstav
    /// som er setningens FØRSTE ord telles IKKE som et egennavn (ambiguøst — kunne bare være vanlig
    /// stor forbokstav ved setningsstart) og gir INGEN kandidat i det hele tatt (verken "virksomhet"
    /// eller "rolle") — det faller ikke tilbake til "rolle", siden det fortsatt HAR stor forbokstav og
    /// dermed ikke oppfyller "rolle"-regelens "liten forbokstav"-vilkår heller. Bevisst redusert recall
    /// for økt presisjon, som spesifisert.
    /// </para>
    /// </summary>
    internal static List<(int Start, int Lengde, string Kategori)> FinnKandidaterITekst(string tekst)
    {
        var funnet = new List<(int, int, string)>();

        foreach (Match m in SuffiksMønster.Matches(tekst))
        {
            var forsteBokstav = tekst[m.Index];
            if (char.IsUpper(forsteBokstav))
            {
                if (!ErSetningsstart(tekst, m.Index)) funnet.Add((m.Index, m.Length, "virksomhet"));
                // else: setningsstart — ambiguøst, ingen kandidat (se metodekommentaren).
            }
            else
            {
                funnet.Add((m.Index, m.Length, "rolle"));
            }
        }

        foreach (Match m in FasteRollerMønster.Matches(tekst))
        {
            funnet.Add((m.Index, m.Length, "rolle"));
        }

        return funnet;
    }

    /// <summary>
    /// Skanner bakover fra <paramref name="index"/>, hopper over whitespace, og ser på det første
    /// ikke-whitespace-tegnet før det. Start av teksten ELLER et setningsavsluttende tegn (<c>. ! ?</c>)
    /// rett før → setningsstart. Ren tegnbasert heuristikk (ingen ekte språklig setningsparsing) —
    /// dokumentert begrensning: et paragraf-/leddnummer som "(1) " rett før treffet regnes IKKE som en
    /// setningsavslutning (parentesen er ikke i tegnlisten over), så "(1) Advokattilsynet utsteder …"
    /// telles som MIDT i en setning (ikke setningsstart) — et bevisst enkelt valg konsistent med at hele
    /// mekanismen er "ren tekstanalyse (regex)", ikke ekte NLP-setningsgrensededeksjon.
    /// </summary>
    private static bool ErSetningsstart(string tekst, int index)
    {
        var i = index - 1;
        while (i >= 0 && char.IsWhiteSpace(tekst[i])) i--;
        return i < 0 || tekst[i] is '.' or '!' or '?';
    }

    /// <summary>Idempotent — samme (rettskilde, node, START-posisjon) gir samme rad tilbake i stedet
    /// for et duplikat, uansett status (samme mønster som <see cref="VirksomhetKandidatTjeneste.OpprettEllerFinnAsync"/>).</summary>
    public async Task<NavnekandidatEntitet> OpprettEllerFinnAsync(
        string foreslattTekst, string kategori, Guid rettskildeId, string nodeEid, int startOffset, int endOffset,
        string opprettetAv, CancellationToken ct = default)
    {
        var eksisterende = await db.Navnekandidater.FirstOrDefaultAsync(
            k => k.RettskildeId == rettskildeId && k.NodeEid == nodeEid && k.StartOffset == startOffset, ct);
        if (eksisterende is not null) return eksisterende;

        if (kategori is not ("virksomhet" or "rolle"))
        {
            throw new ArgumentException($"Ukjent kategori '{kategori}'. Gyldige verdier: 'virksomhet', 'rolle'.");
        }
        var node = await db.RettskildeNoder.FirstOrDefaultAsync(n => n.RettskildeId == rettskildeId && n.Eid == nodeEid, ct);
        if (node is null)
        {
            throw new ArgumentException($"Fant ingen rettskilde-node med eId '{nodeEid}' i rettskilde '{rettskildeId}'. Ingen gjettet fallback.");
        }
        if (endOffset <= startOffset || startOffset < 0 || endOffset > (node.Tekst?.Length ?? 0))
        {
            throw new ArgumentException(
                $"Ugyldig tegnintervall [{startOffset}, {endOffset}) for node '{nodeEid}' (tekstlengde {node.Tekst?.Length ?? 0}).");
        }

        var kandidat = new NavnekandidatEntitet
        {
            Id = Guid.NewGuid(),
            ForeslattTekst = foreslattTekst,
            Kategori = kategori,
            RettskildeId = rettskildeId,
            NodeEid = nodeEid,
            StartOffset = startOffset,
            EndOffset = endOffset,
            Status = "Venter",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Navnekandidater.Add(kandidat);
        await db.SaveChangesAsync(ct);
        return kandidat;
    }

    /// <summary>Full liste, valgfritt filtrert på status og/eller kategori. <paramref name="status"/> =
    /// <c>null</c> betyr ALLE statuser (samme eksplisitte "ingen stille standard"-mønster som
    /// <see cref="VirksomhetKandidatTjeneste.ListerAsync"/>).</summary>
    public Task<List<NavnekandidatEntitet>> ListerAsync(
        string? status = null, string? kategori = null, Guid? rettskildeId = null, CancellationToken ct = default)
    {
        var spørring = db.Navnekandidater.AsQueryable();
        if (status is not null) spørring = spørring.Where(k => k.Status == status);
        if (kategori is not null) spørring = spørring.Where(k => k.Kategori == kategori);
        if (rettskildeId is not null) spørring = spørring.Where(k => k.RettskildeId == rettskildeId);
        return spørring.OrderBy(k => k.RettskildeId).ThenBy(k => k.NodeEid).ThenBy(k => k.StartOffset).ToListAsync(ct);
    }

    /// <summary>
    /// Godkjenner kandidaten. Oppførsel avhenger av <see cref="NavnekandidatEntitet.Kategori"/> (se
    /// klassekommentaren på <see cref="NavnekandidatEntitet"/> for HVORFOR):
    /// <list type="bullet">
    /// <item><c>"rolle"</c> — oppretter et EKTE rollebegrep direkte
    /// (<see cref="VirksomhetsbegrepTjeneste.OpprettRollebegrepAsync"/>, <c>Term</c>=<see cref="NavnekandidatEntitet.ForeslattTekst"/>,
    /// <c>LovkildeId</c>=kandidatens <see cref="NavnekandidatEntitet.RettskildeId"/>) — alt godkjenningen
    /// trenger er allerede kjent fra selve kandidaten.</item>
    /// <item><c>"virksomhet"</c> — oppretter INGENTING. Godkjenning her betyr kun "reelt navn, verdt å
    /// følge opp" — selve koblingen til en konkret <see cref="Virksomhet"/> (ny eller eksisterende)
    /// krever et menneske og skjer via den eksisterende navneform-tilleggsflyten i
    /// <c>VirksomhetDetalj.tsx</c>/<c>VirksomhetsbegrepTjeneste.OpprettVirksomhetsbegrepAsync</c>.</item>
    /// </list>
    /// Hvis rollebegrep-opprettelsen kaster (f.eks. en rad med samme (Term, LovkildeId) allerede finnes
    /// — <see cref="VirksomhetsbegrepTjeneste.OpprettRollebegrepAsync"/> sitt eget "ingen gjettet
    /// fallback"-vern), forblir kandidatens status <c>"Venter"</c> og feilen forplantes uendret —
    /// samme "ikke sett status før den faktiske handlingen lyktes"-prinsipp som
    /// <see cref="VirksomhetKandidatTjeneste.GodkjennAsync"/>.
    /// </summary>
    public async Task<NavnekandidatEntitet?> GodkjennAsync(Guid id, string behandletAv, CancellationToken ct = default)
    {
        var kandidat = await db.Navnekandidater.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kandidat is null) return null;
        if (kandidat.Status != "Venter")
        {
            throw new ArgumentException(
                $"Kandidaten har status '{kandidat.Status}' — kan kun godkjenne kandidater med status 'Venter'.");
        }

        if (kandidat.Kategori == "rolle")
        {
            await virksomhetsbegrep.OpprettRollebegrepAsync(kandidat.RettskildeId, kandidat.ForeslattTekst, behandletAv, ct);
        }
        // "virksomhet": ingen entitet opprettes her — se metodekommentaren.

        kandidat.Status = "Godkjent";
        kandidat.BehandletAv = behandletAv;
        kandidat.BehandletTidspunkt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return kandidat;
    }

    public async Task<NavnekandidatEntitet?> AvvisAsync(Guid id, string behandletAv, CancellationToken ct = default)
    {
        var kandidat = await db.Navnekandidater.FirstOrDefaultAsync(k => k.Id == id, ct);
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
}

/// <summary>Oppsummering av ett sveip — <see cref="AntallTreffFunnet"/> teller ALLE mønstertreff (også
/// de som allerede fantes som kandidat fra et tidligere sveip, eller som ble filtrert bort fordi de
/// allerede er dekket av et eksisterende Begrep — se <see cref="NavnekandidatOppdagelseTjeneste.SveipAsync"/>
/// for at "dekket"-filtreringen skjer FØR denne telles opp, altså telles et dekket treff IKKE med her),
/// <see cref="AntallNyeKandidater"/> kun de som faktisk ble en NY rad i køen denne kjøringen.</summary>
public sealed record NavnekandidatSveipResultat(int AntallTreffFunnet, int AntallNyeKandidater);
