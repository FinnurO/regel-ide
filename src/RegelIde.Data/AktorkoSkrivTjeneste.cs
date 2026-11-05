using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>[Ny, issue #440 PR 2] Hva et navn i aktørkøen kan bli ved godkjenning. Ingenting er forhåndsvalgt: typen velges alltid.</summary>
public static class Aktorkotyper
{
    public const string Aktor = "aktor";
    public static readonly string[] Alle = [Aktor, Nodetyper.Klasse, Nodetyper.Rolle, Nodetyper.Omrade];

    public static string Tekst(string type) => type == Aktor ? "aktør" : Nodetyper.Visningsnavn(type);

    /// <summary>Bestemt form med stor forbokstav: «Aktøren», «Klassen», «Rollen», «Området».</summary>
    public static string Bestemt(string type) => type switch
    {
        Aktor => "Aktøren",
        Nodetyper.Klasse => "Klassen",
        Nodetyper.Rolle => "Rollen",
        Nodetyper.Omrade => "Området",
        _ => type,
    };
}

/// <summary>Valget av hjemmel ved godkjenning: et av hjemmelforslagene etter regelen, eller «hjemmel mangler».</summary>
public static class Aktorkohjemmel
{
    public const string Forslag = "forslag";
    public const string Mangler = "mangler";
    /// <summary>[Ny, Johanns avgjørelse 2026-11-05] Hjemmelen i forhåndsklassifiseringen for navnet (<see cref="Forhandsklassifisering"/>).
    /// eId-en må være nøyaktig filas for nøkkelen; rettskilden og kunnskapsstatusen tas fra fila og korpus, ikke fra forespørselen.</summary>
    public const string Forhandsklassifisering = "forhandsklassifisering";
}

/// <summary>
/// [Ny, issue #440 PR 2] Godkjenningen av ett navn i aktørkøen. Alt er eksplisitt valgt av et menneske; tjenesten fyller ikke inn noe.
/// </summary>
/// <param name="Type">En av <see cref="Aktorkotyper.Alle"/>.</param>
/// <param name="Navn">Skrivemåten som blir navnet: en av skrivemåtene i teksten (ingen omskriving, CLAUDE.md §8).</param>
/// <param name="Hjemmel"><see cref="Aktorkohjemmel.Forslag"/> (med <paramref name="HjemmelRettskildeId"/> og <paramref name="HjemmelEid"/> fra
/// et av forslagene) eller <see cref="Aktorkohjemmel.Mangler"/>.</param>
/// <param name="Godkjenning"><c>faglig_vurdert</c> eller <c>godkjent</c>.</param>
/// <param name="GjelderILovkildeId">For klasse, rolle eller område uten hjemmel: rettskilden begrepet gjelder i (en der navnet står).</param>
/// <param name="GjelderFast">For klasse, rolle eller område uten hjemmel: fast begrep for hele korpuset i stedet for én rettskilde.</param>
/// <param name="ForventetForekomster">Antall forekomster brukeren så. Er det endret siden, avvises godkjenningen (409), så ingen godkjenner
/// noe annet enn det som ble vist.</param>
/// <param name="Tolkningsargumenter">[Ny, #440, 2026-11-05] Når hjemmelen fra forhåndsklassifiseringen er <c>fortolkning</c>: argumentene
/// (lukket liste, <see cref="Strukturkanter.Tolkningsargumenter"/>). Fila har dem ikke, så mennesket velger; de gjettes ikke.</param>
public sealed record AktorkoGodkjenning(
    string? Type, string? Navn, string? Hjemmel, Guid? HjemmelRettskildeId, string? HjemmelEid, string? Godkjenning, string? Begrunnelse = null,
    Guid? GjelderILovkildeId = null, bool? GjelderFast = null, int? ForventetForekomster = null, IReadOnlyList<string>? Tolkningsargumenter = null);

/// <summary>[Ny, #440 PR 2] Koble navnet til en aktør som finnes, som ny navneform.</summary>
/// <param name="Navneformgrunn">Påkrevd: gjeldende, utgatt, kortform, feilskriving eller parallellnavn.</param>
public sealed record AktorkoKobling(Guid? VirksomhetId, string? Navn, string? Navneformgrunn, int? ForventetForekomster = null);

/// <summary>
/// [Ny, #440 PR 2] Kvitteringen etter en skrivende handling i aktørkøen: hva som ble opprettet eller endret, i klart språk og med id-ene som
/// trengs for lenker. <see cref="Varsler"/> er det brukeren bør vite (utgått navn, kortform, en hjemmel som ikke viser navnet).
/// </summary>
public sealed record AktorkoKvittering(
    string Handling, string Nokkel, int Forekomster, int Tagget, int IkkeTagget, IReadOnlyList<string> Linjer, IReadOnlyList<string> Varsler,
    Guid? VirksomhetId = null, string? VirksomhetNavn = null, Guid? BegrepId = null, Guid? NavneformId = null, string? HjemmelArt = null);

/// <summary>[Ny, #440 PR 2] Tørrkjøring eller skriving av «Tagg alle som generisk omtale» for ett ord.</summary>
/// <param name="Forekomster">Ventende forekomster av ordet som er med (rutene generisk ord og aktørkøen).</param>
/// <param name="Utenfor">Forekomster av ordet på andre ruter (kjent aktør, kjent klasse eller rolle): de er ikke med.</param>
/// <param name="NavneformFinnes">Den generiske navneformen finnes alt (ellers opprettes den).</param>
/// <param name="Sperre">Ordet kan ikke bli generisk (det er alt en navneform for en bestemt aktør). Da skrives ingenting.</param>
public sealed record GeneriskOmtaleRapport(
    bool Skrevet, string Ord, int Forekomster, int Rettskilder, int Utenfor, IReadOnlyDictionary<string, int> UtenforPerRute, bool NavneformFinnes,
    int Tagget, int IkkeTagget, string? Sperre);

/// <summary>Navnet er endret siden brukeren så det, eller en forekomst er alt behandlet. Gir 409.</summary>
public sealed class AktorkoKonfliktException(string melding) : Exception(melding);

/// <summary>
/// [Ny, issue #440 PR 2, Johanns avgjørelser 2026-10-30 og 2026-11-01] Skrivingen i aktørkøen. Hver handling er ÉN transaksjon: feiler et
/// steg, rulles alt tilbake, og navnet står i køen som før.
/// <list type="bullet">
/// <item><b>Godkjenn</b> (<see cref="GodkjennAsync"/>): aktøren (eller klassen, rollen eller området), navneformen med hjemmel eller «hjemmel
/// mangler», tagger for alle forekomstene, og kandidatene settes til Godkjent. Ingenting er forhåndsvalgt.</item>
/// <item><b>Ikke en aktør</b> (<see cref="IkkeAktorAsync"/>): alle forekomstene avvises med begrunnelse. Radene slettes ikke.</item>
/// <item><b>Koble</b> (<see cref="KobleAsync"/>): navnet blir ny navneform på en aktør som finnes, med grunn.</item>
/// <item><b>Skill ut / ta med / tilbakestill</b>: grupperingen overstyres per forekomst (<see cref="NavnekandidatEntitet.AktorkoGruppe"/>).</item>
/// <item><b>Tagg alle som generisk omtale</b> (<see cref="TaggSomGeneriskAsync"/>): per ord, tørrkjøring først.</item>
/// </list>
/// </summary>
public sealed class AktorkoSkrivTjeneste(
    RegelIdeDbContext db, AktorkoTjeneste ko, NavnekandidatOppdagelseTjeneste kandidater, VirksomhetsbegrepTjeneste begreper,
    AktoridentitetTjeneste identitet)
{
    public const string Oppdagelseskilde = "aktorko";

    // ------------------------------------------------------------------------------------------------ Godkjenn

    public async Task<AktorkoKvittering?> GodkjennAsync(string nokkel, AktorkoGodkjenning valg, string bruker, CancellationToken ct = default)
    {
        var g = await ko.FinnGruppeAsync(nokkel, ct);
        if (g is null) return null;
        KrevUendret(g, valg.ForventetForekomster);

        var type = valg.Type?.Trim();
        if (type is null || !Aktorkotyper.Alle.Contains(type))
        {
            throw new ArgumentException($"Velg hva navnet er: {string.Join(", ", Aktorkotyper.Alle)}. Ingenting er forhåndsvalgt.");
        }
        // [ENDRET, Johanns avgjørelse 2026-11-05] Navnet kan også være navnet i forhåndsklassifiseringen (grunnformen, «foreldreråd»),
        // kontrollert mot fila for nøkkelen. Ellers en skrivemåte fra teksten, som før.
        var post = Forhandsklassifisering.Finn(g);
        var navnFraForslag = post is { NyNode: { } nyNode } && Forhandsklassifisering.Handling(post) == Forhandshandling.Godkjenn
                             && Normaliser(valg.Navn) == nyNode && g.Varianter.SelectMany(v => v.Skrivemater).All(s => s.Tekst != nyNode);
        var navn = navnFraForslag ? post!.NyNode! : KrevSkrivemate(g, valg.Navn);
        if (valg.Godkjenning is not (Strukturkanter.FagligVurdert or Strukturkanter.Godkjent))
        {
            throw new ArgumentException("Velg hvor sikkert det er: faglig_vurdert eller godkjent. Ingenting er forhåndsvalgt.");
        }
        if (valg.Tolkningsargumenter is { Count: > 0 } && valg.Hjemmel != Aktorkohjemmel.Forhandsklassifisering)
        {
            throw new ArgumentException("Tolkningsargumenter brukes bare med hjemmelen fra forhåndsklassifiseringen når den er fortolkning.");
        }

        Hjemmelforslag? forslag = null;
        // Hjemmelen som lagres, uansett hvor den kom fra: regelens forslag eller forhåndsklassifiseringen.
        Guid? hjemmelRettskilde = null;
        string? hjemmelEid = null;
        Utsagnsgrunnlag grunnlag;
        var egen = string.IsNullOrWhiteSpace(valg.Begrunnelse) ? "" : " " + valg.Begrunnelse.Trim();
        if (valg.Hjemmel == Aktorkohjemmel.Forslag)
        {
            var alle = await ko.HjemmelforslagAsync(g, ct);
            forslag = alle.FirstOrDefault(f => f.RettskildeId == valg.HjemmelRettskildeId && f.Eid == valg.HjemmelEid?.Trim())
                ?? throw new ArgumentException("Hjemmelen er ikke et av forslagene etter regelen for dette navnet. Velg et forslag, eller «hjemmel "
                                               + "mangler». En annen hjemmel registreres på aktørsiden. Ingen gjettet fallback.");
            grunnlag = new Utsagnsgrunnlag
            {
                Grunnlag = Utsagnsgrunnlag.Hjemmel, HjemmelRettskildeId = forslag.RettskildeId, HjemmelEid = forslag.Eid,
                Kunnskapsstatus = forslag.Kunnskapsstatus, Godkjenning = valg.Godkjenning!,
                Begrunnelse = $"Hjemmelforslag etter regelen i aktørkøen (docs/33 §5.16): «{Aktorkoregler.UttrykkTekst(forslag.Uttrykk)}» med "
                              + $"«{forslag.Treff}» som kjerne i frasen.{egen}",
            };
            (hjemmelRettskilde, hjemmelEid) = (forslag.RettskildeId, forslag.Eid);
        }
        else if (valg.Hjemmel == Aktorkohjemmel.Forhandsklassifisering)
        {
            // [Ny, Johanns avgjørelse 2026-11-05] Hjemmelen fra fila, aldri fri: eId-en må være filas for nøkkelen, rettskilden er den
            // visningen viser (ForhandBestemmelseAsync), og kunnskapsstatusen og sitatet kommer fra fila.
            var h = post?.Hjemmel ?? throw new ArgumentException(
                $"«{g.Navn}» har ingen hjemmel i forhåndsklassifiseringen. Velg et av regelens forslag, eller «hjemmel mangler».");
            if (valg.HjemmelEid?.Trim() != h.Eid)
            {
                throw new ArgumentException($"Hjemmelen er ikke forhåndsklassifiseringens for «{g.Navn}» ({h.Eid}). Ingen annen hjemmel kan lagres herfra.");
            }
            var b = await ko.ForhandBestemmelseAsync(g, h, ct) ?? throw new ArgumentException(
                $"Bestemmelsen {h.Eid} står ikke entydig i korpus, og fila har ingen kilde utenfor korpus. Den registreres på aktørsiden eller begrepssiden.");
            if (valg.HjemmelRettskildeId is { } r && r != b.RettskildeId)
            {
                throw new ArgumentException("Rettskilden er ikke den bestemmelsen står i. Ingen gjettet fallback.");
            }
            if (valg.Tolkningsargumenter is { Count: > 0 } && h.Kunnskapsstatus != Strukturkanter.Fortolkning)
            {
                throw new ArgumentException("Tolkningsargumenter brukes bare når hjemmelen er fortolkning.");
            }
            var sitat = string.IsNullOrWhiteSpace(h.Sitat) ? b.Tekst : h.Sitat;
            grunnlag = new Utsagnsgrunnlag
            {
                Grunnlag = Utsagnsgrunnlag.Hjemmel, HjemmelRettskildeId = b.RettskildeId, HjemmelEid = h.Eid, Kunnskapsstatus = h.Kunnskapsstatus,
                Godkjenning = valg.Godkjenning!, Tolkningsargumenter = valg.Tolkningsargumenter,
                Begrunnelse = $"Hjemmel fra forhåndsklassifiseringen ({Forhandsklassifisering.Kilde}, n{post!.Nr}; KI-agent og juristrunde, #440)"
                              + (sitat is null ? "." : $": «{sitat}».") + (post.HjemmelMerknad is { } m && m != "ikke relevant" ? $" {m}." : "") + egen,
            };
            (hjemmelRettskilde, hjemmelEid) = (b.RettskildeId, h.Eid);
        }
        else if (valg.Hjemmel == Aktorkohjemmel.Mangler)
        {
            grunnlag = new Utsagnsgrunnlag
            {
                Grunnlag = Utsagnsgrunnlag.Mangler, Kunnskapsstatus = Strukturkanter.Uavklart, Godkjenning = valg.Godkjenning!,
                Begrunnelse = $"Hjemmel for aktøren mangler: {AktorkoTjeneste.Hullmerknad} (aktørkøen, docs/33 §5.16).{egen}",
            };
        }
        else
        {
            throw new ArgumentException("Velg hjemmel: et av forslagene, forhåndsklassifiseringens, eller «hjemmel mangler». Ingenting er forhåndsvalgt.");
        }
        grunnlag.Valider();

        // Klasse, rolle eller område: hvor begrepet gjelder. Med hjemmel er det rettskilden hjemmelen står i; uten må det velges.
        Guid? lov = null;
        var fast = false;
        if (type != Aktorkotyper.Aktor)
        {
            var rettskilder = g.Forekomster.Select(f => f.RettskildeId).ToHashSet();
            if (hjemmelRettskilde is { } hr)
            {
                lov = hr;
            }
            else if (valg.GjelderFast == true)
            {
                fast = true;
            }
            else if (valg.GjelderILovkildeId is { } l && rettskilder.Contains(l))
            {
                lov = l;
            }
            else
            {
                throw new ArgumentException($"Velg hvor {Aktorkotyper.Bestemt(type).ToLowerInvariant()} gjelder: en rettskilde der navnet står, eller hele korpuset. "
                                            + "Ingenting er forhåndsvalgt.");
            }
        }

        var rader = await LastVentendeAsync(g, ct);
        return await IEnTransaksjonAsync(async () =>
        {
            var linjer = new List<string>();
            var varsler = new List<string>();
            var hjemmeltekst = hjemmelRettskilde is not { } hrid ? "hjemmel mangler" : $"{await TittelAsync(hrid, ct)} {AktorkoTjeneste.Bestemmelse(hjemmelEid!) ?? hjemmelEid}";
            if (navnFraForslag) linjer.Add($"Navnet «{navn}» er navnet i forhåndsklassifiseringen (står ikke i teksten); forekomstene tagges mot det.");
            Guid? virksomhetId = null, begrepId = null, navneformId = null;
            string? hjemmelArt = null;
            var tagget = 0;

            if (type == Aktorkotyper.Aktor)
            {
                var v = new Virksomhet { Id = Guid.NewGuid(), Navn = navn, Aktiv = true, OpprettetTidspunkt = DateTimeOffset.UtcNow };
                db.Virksomheter.Add(v);
                await db.SaveChangesAsync(ct);
                virksomhetId = v.Id;
                linjer.Add($"Aktøren «{navn}» er opprettet (uten organisasjonsnummer; navnet kommer fra lovteksten).");

                // Formene som kan vise navnet i hjemmelen: regelens treff, eller (forhåndsklassifiseringen) skrivemåtene i teksten.
                IReadOnlyCollection<string>? former = forslag is not null ? [forslag.Treff]
                    : hjemmelEid is not null ? g.Varianter.SelectMany(x => x.Skrivemater).Select(x => x.Tekst).ToList() : null;
                var resultat = await identitet.RegistrerNavnetSomGjeldendeAsync(v.Id, grunnlag, bruker, ct, former);
                var navneform = resultat.Navneform ?? throw new InvalidOperationException(resultat.Merknad ?? "Navneformen ble ikke registrert.");
                if (forslag is not null && resultat.Merknad is null)
                {
                    navneform.HjemmelArt = "oppretting";
                    hjemmelArt = navneform.HjemmelArt;
                }
                navneformId = navneform.Id;
                linjer.Add($"Navneformen «{navneform.Term}» er gjeldende, med {(navneform.LovreferanseEid is null ? "«hjemmel mangler»" : hjemmeltekst)} "
                           + $"({KunnskapsstatusTekst(navneform.Kunnskapsstatus)}, {GodkjenningTekst(navneform.Godkjenning)})"
                           + (hjemmelArt is null ? "." : ", hjemmelens art: oppretting."));
                if (resultat.Merknad is not null) varsler.Add(resultat.Merknad);

                foreach (var k in rader)
                {
                    k.Kategori = "virksomhet";
                    if (await kandidater.OpprettEllerKobleVirksomhetTaggAsync(k, navneform.Id, bruker, ct) is not null) tagget++;
                }
            }
            else
            {
                var (begrep, ny) = fast
                    ? await begreper.OpprettEllerGjenbrukFastGruppebegrepAsync(type, navn, bruker, null, ct)
                    : await begreper.OpprettEllerGjenbrukGruppebegrepAsync(type, lov!.Value, navn, bruker, hjemmelEid, ct);
                begrepId = begrep.Id;
                var hvor = fast ? "for hele korpuset" : $"i {await TittelAsync(lov!.Value, ct)}";
                if (ny)
                {
                    grunnlag.SkrivTil(begrep, Oppdagelseskilde);
                    if (forslag is not null) begrep.HjemmelArt = hjemmelArt = "oppretting";
                    begrep.SistEndretAv = bruker;
                    begrep.SistEndretTidspunkt = DateTimeOffset.UtcNow;
                    linjer.Add($"{Aktorkotyper.Bestemt(type)} «{navn}» er opprettet {hvor}, med {hjemmeltekst} "
                               + $"({KunnskapsstatusTekst(begrep.Kunnskapsstatus)}, {GodkjenningTekst(begrep.Godkjenning)})"
                               + (hjemmelArt is null ? "." : ", hjemmelens art: oppretting."));
                }
                else
                {
                    linjer.Add($"{Aktorkotyper.Bestemt(type)} «{begrep.Term}» fantes alt {hvor}. Forekomstene er koblet til den; grunnlaget den "
                               + "har, er ikke endret.");
                }
                foreach (var k in rader)
                {
                    k.Kategori = type;
                    if (await kandidater.OpprettDepartementTaggHvisMuligAsync(k, begrep.Id, bruker, ct) is not null) tagget++;
                }
            }

            Godkjenn(rader, bruker);
            await db.SaveChangesAsync(ct);
            linjer.Add(TaggLinje(rader.Count, tagget, g));
            if (tagget < rader.Count) varsler.Add(IkkeTaggetForklaring(rader.Count - tagget));
            return new AktorkoKvittering("godkjent", g.Nokkel, rader.Count, tagget, rader.Count - tagget, linjer, varsler,
                virksomhetId, virksomhetId is null ? null : navn, begrepId, navneformId, hjemmelArt);
        }, ct);
    }

    // ------------------------------------------------------------------------------------------------ Ikke en aktør

    public async Task<AktorkoKvittering?> IkkeAktorAsync(string nokkel, string? begrunnelse, int? forventet, string bruker, CancellationToken ct = default)
    {
        var g = await ko.FinnGruppeAsync(nokkel, ct);
        if (g is null) return null;
        KrevUendret(g, forventet);
        if (string.IsNullOrWhiteSpace(begrunnelse))
        {
            throw new ArgumentException("«Ikke en aktør» krever en begrunnelse (#385 AC7).");
        }
        var rader = await LastVentendeAsync(g, ct);
        return await IEnTransaksjonAsync(async () =>
        {
            foreach (var k in rader)
            {
                k.Status = "Avvist";
                k.AvvisningBegrunnelse = begrunnelse.Trim();
                k.BehandletAv = bruker;
                k.BehandletTidspunkt = DateTimeOffset.UtcNow;
            }
            await db.SaveChangesAsync(ct);
            return new AktorkoKvittering("ikke_aktor", g.Nokkel, rader.Count, 0, 0,
                [$"«{g.Navn}» er avvist som «ikke en aktør» for alle {rader.Count} forekomstene, med begrunnelsen «{begrunnelse.Trim()}».",
                 "Radene er ikke slettet; de står i historikken med begrunnelsen."], []);
        }, ct);
    }

    // ------------------------------------------------------------------------------------------------ Koble til en aktør som finnes

    /// <summary>Varselet som hører til en navneformgrunn: utgått navn og kortform er ikke aktørens fulle navn i dag.</summary>
    public static string? Grunnvarsel(string grunn, string navn, string aktor) => grunn switch
    {
        "utgatt" => $"«{navn}» registreres som UTGÅTT navn på {aktor}. Forekomstene peker på aktøren, men navnet er ikke aktørens navn i dag.",
        "kortform" => $"«{navn}» registreres som KORTFORM for {aktor}. Kortformen er ikke aktørens fulle navn.",
        _ => null,
    };

    public async Task<AktorkoKvittering?> KobleAsync(string nokkel, AktorkoKobling valg, string bruker, CancellationToken ct = default)
    {
        var g = await ko.FinnGruppeAsync(nokkel, ct);
        if (g is null) return null;
        KrevUendret(g, valg.ForventetForekomster);
        var navn = KrevSkrivemate(g, valg.Navn);
        if (valg.Navneformgrunn is null || !VirksomhetsbegrepTjeneste.Navneformgrunner.Contains(valg.Navneformgrunn))
        {
            throw new ArgumentException($"Velg navneformgrunn: {string.Join(", ", VirksomhetsbegrepTjeneste.Navneformgrunner)}. Ingenting er forhåndsvalgt.");
        }
        var v = await db.Virksomheter.FirstOrDefaultAsync(x => x.Id == valg.VirksomhetId, ct)
            ?? throw new ArgumentException($"Fant ingen aktør med id '{valg.VirksomhetId}'. Ingen gjettet fallback.");
        if (v.ErstattetAvId is not null)
        {
            throw new ArgumentException($"«{v.Navn}» er erstattet som duplikat. Koble til aktøren som erstatter den.");
        }
        var rader = await LastVentendeAsync(g, ct);
        return await IEnTransaksjonAsync(async () =>
        {
            var linjer = new List<string>();
            var varsler = new List<string>();
            var lav = navn.ToLower();
            var navneform = await db.Begreper.FirstOrDefaultAsync(b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == v.Id
                && b.Entitetsstatus == "gjeldende" && b.Godkjenning != Strukturkanter.Avvist && b.Term.ToLower() == lav, ct);
            if (navneform is null)
            {
                navneform = await begreper.OpprettVirksomhetsbegrepAsync(v.Id, navn, bruker, null, valg.Navneformgrunn, ct);
                navneform.OppdagelsesKilde = Oppdagelseskilde;
                linjer.Add($"«{navn}» er ny navneform på {v.Navn}, med grunnen {valg.Navneformgrunn}.");
            }
            else
            {
                linjer.Add($"«{navneform.Term}» fantes alt som navneform på {v.Navn}"
                           + (navneform.Navneformgrunn is null ? "." : $" ({navneform.Navneformgrunn}); grunnen er ikke endret."));
                if (navneform.Navneformgrunn is not null && navneform.Navneformgrunn != valg.Navneformgrunn)
                {
                    varsler.Add($"Navneformen har grunnen {navneform.Navneformgrunn}, ikke {valg.Navneformgrunn}. Den endres på aktørsiden.");
                }
            }
            if (Grunnvarsel(navneform.Navneformgrunn ?? valg.Navneformgrunn, navneform.Term, v.Navn) is { } varsel) varsler.Add(varsel);
            if (v.Livsstatus == "nedlagt") varsler.Add($"{v.Navn} er nedlagt. Forekomstene peker på en aktør som ikke finnes i dag.");

            var tagget = 0;
            foreach (var k in rader)
            {
                k.Kategori = "virksomhet";
                if (await kandidater.OpprettEllerKobleVirksomhetTaggAsync(k, navneform.Id, bruker, ct) is not null) tagget++;
            }
            Godkjenn(rader, bruker);
            await db.SaveChangesAsync(ct);
            linjer.Add(TaggLinje(rader.Count, tagget, g));
            if (tagget < rader.Count) varsler.Add(IkkeTaggetForklaring(rader.Count - tagget));
            return new AktorkoKvittering("koblet", g.Nokkel, rader.Count, tagget, rader.Count - tagget, linjer, varsler, v.Id, v.Navn, null, navneform.Id);
        }, ct);
    }

    // ------------------------------------------------------------------------------------------------ Grupperingen: skill ut, ta med, tilbakestill

    /// <summary>Skill ut en variant som eget navn. Forekomstene av varianten får <see cref="NavnekandidatEntitet.AktorkoGruppe"/> = varianten.</summary>
    public async Task<AktorkoKvittering?> SkillUtAsync(string nokkel, string? variant, string bruker, CancellationToken ct = default)
    {
        var g = await ko.FinnGruppeAsync(nokkel, ct);
        if (g is null) return null;
        var v = Aktorkoregler.Normaliser(variant ?? "");
        if (g.Varianter.All(x => x.Normalisert != v)) throw new ArgumentException($"«{variant}» er ikke en variant av «{g.Navn}».");
        if (g.Varianter.Count < 2) throw new ArgumentException($"«{g.Navn}» har bare én variant; det er ingenting å skille ut.");
        var ider = g.Forekomster.Where(f => f.Normalisert == v).Select(f => f.NavnekandidatId).ToList();
        return await IEnTransaksjonAsync(async () =>
        {
            var antall = await SettGruppeAsync(ider, v, ct);
            return new AktorkoKvittering("skilt_ut", v, antall, 0, 0,
                [$"«{v}» er skilt ut fra «{g.Navn}» og står som eget navn i køen ({antall} forekomster). Valget gjelder også nye forekomster av ordet."], []);
        }, ct);
    }

    /// <summary>Ta med et ord fra et annet navn i køen (typisk et genitivforslag) i dette navnet. Navnets egne forekomster festes også, så
    /// navnet ikke skifter nøkkel når tallene endrer seg.</summary>
    public async Task<AktorkoKvittering?> TaMedAsync(string nokkel, string? variant, string bruker, CancellationToken ct = default)
    {
        var g = await ko.FinnGruppeAsync(nokkel, ct);
        if (g is null) return null;
        var v = Aktorkoregler.Normaliser(variant ?? "");
        if (g.Varianter.Any(x => x.Normalisert == v)) throw new ArgumentException($"«{variant}» står alt under «{g.Navn}».");
        var fra = await ko.FinnGruppeAsync(v, ct)
            ?? throw new ArgumentException($"«{variant}» står ikke i aktørkøen. Bare ord i køen kan tas med.");
        var ider = fra.Forekomster.Where(f => f.Normalisert == v).Select(f => f.NavnekandidatId)
            .Concat(g.Forekomster.Select(f => f.NavnekandidatId)).ToList();
        return await IEnTransaksjonAsync(async () =>
        {
            await SettGruppeAsync(ider, g.Nokkel, ct);
            var antall = fra.Forekomster.Count(f => f.Normalisert == v);
            return new AktorkoKvittering("tatt_med", g.Nokkel, g.Forekomster.Count + antall, 0, 0,
                [$"«{v}» ({antall} forekomster) er tatt med i «{g.Navn}»" + (fra.Nokkel == v ? "." : $", fra «{fra.Navn}»."),
                 "Varianten er merket «tatt med av et menneske». Grupperingen kan tilbakestilles."], []);
        }, ct);
    }

    /// <summary>Tilbakestill grupperingen for navnet: alle forekomstene beregnes av regelen igjen.</summary>
    public async Task<AktorkoKvittering?> TilbakestillAsync(string nokkel, string bruker, CancellationToken ct = default)
    {
        var g = await ko.FinnGruppeAsync(nokkel, ct);
        if (g is null) return null;
        var ider = g.Forekomster.Select(f => f.NavnekandidatId).ToList();
        return await IEnTransaksjonAsync(async () =>
        {
            var antall = await SettGruppeAsync(ider, null, ct);
            return new AktorkoKvittering("tilbakestilt", g.Nokkel, antall, 0, 0,
                [$"Grupperingen for «{g.Navn}» er tilbakestilt: {antall} forekomster grupperes av regelen igjen."], []);
        }, ct);
    }

    private async Task<int> SettGruppeAsync(List<Guid> ider, string? gruppe, CancellationToken ct)
    {
        var rader = await db.Navnekandidater.Where(k => ider.Contains(k.Id) && k.Status == "Venter").ToListAsync(ct);
        foreach (var k in rader) k.AktorkoGruppe = gruppe;
        await db.SaveChangesAsync(ct);
        return rader.Count;
    }

    // ------------------------------------------------------------------------------------------------ Tagg alle som generisk omtale

    /// <summary>
    /// [Johanns avgjørelse 2026-10-30] «Tagg alle som generisk omtale» for ett ord: alle ventende forekomster av NØYAKTIG det ordet (ikke
    /// bøyningsformer: «et departement» er ikke «departementet») på rutene «generisk ord» og «aktørkøen». Den generiske navneformen opprettes
    /// eller gjenbrukes, hver forekomst tagges mot den, og kandidatene settes til Godkjent. Ingen binding: svaret per rettskilde kommer fra
    /// standardregelen eller en binding (docs/33 §5.10).
    /// <para>
    /// <b>Tørrkjøringen</b> (<paramref name="skriv"/> = false) gjør det samme i en transaksjon som rulles tilbake, så tallene (tagget, ikke tagget)
    /// er de skrivingen ville gitt, ikke et anslag.
    /// </para>
    /// </summary>
    public async Task<GeneriskOmtaleRapport> TaggSomGeneriskAsync(string? ord, bool skriv, string bruker, CancellationToken ct = default)
    {
        var n = Aktorkoregler.Normaliser(ord ?? "");
        if (n.Length == 0) throw new ArgumentException("Ordet mangler.");
        var (med, utenfor) = await ko.ForekomsterAvOrdAsync(n, [Aktorkoruter.Generisk, Aktorkoruter.Aktorko], ct);
        var utenforPerRute = utenfor.GroupBy(x => x.Rute).ToDictionary(x => x.Key, x => x.Count());
        var finnes = await db.Begreper.AnyAsync(b => b.Begrepskategori == "virksomhet" && b.Navneformgrunn == VirksomhetsbegrepTjeneste.Generisk
                                                     && b.Entitetsstatus == "gjeldende" && b.Term == n, ct);
        var bundne = await db.Begreper.Where(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende" && b.VirksomhetReferanseId != null
                                                  && b.Godkjenning != Strukturkanter.Avvist && b.Term.ToLower() == n)
            .Join(db.Virksomheter, b => b.VirksomhetReferanseId, v => v.Id, (b, v) => v.Navn).Take(3).ToListAsync(ct);
        GeneriskOmtaleRapport Rapport(bool skrevet, int tagget, string? sperre) => new(skrevet, n, med.Count,
            med.Select(f => f.RettskildeId).Distinct().Count(), utenfor.Count, utenforPerRute, finnes, tagget, med.Count - tagget, sperre);
        if (bundne.Count > 0 && !finnes)
        {
            return Rapport(false, 0, $"«{n}» er alt en navneform for {string.Join(", ", bundne)}, og kan ikke samtidig være generisk.");
        }
        if (med.Count == 0) return Rapport(false, 0, null);

        var ider = med.Select(f => f.NavnekandidatId).ToList();
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var rader = await db.Navnekandidater.Where(k => ider.Contains(k.Id) && k.Status == "Venter").ToListAsync(ct);
            var (navneform, _) = await new GeneriskNavneformTjeneste(db).OpprettEllerFinnAsync(n, bruker, ct);
            var tagget = 0;
            foreach (var k in rader)
            {
                if (await kandidater.OpprettEllerKobleVirksomhetTaggAsync(k, navneform.Id, bruker, ct) is not null) tagget++;
            }
            Godkjenn(rader, bruker);
            await db.SaveChangesAsync(ct);
            if (skriv && tx is not null) await tx.CommitAsync(ct);
            else if (tx is not null) await tx.RollbackAsync(ct);
            if (!skriv) db.ChangeTracker.Clear();
            return Rapport(skriv, tagget, null);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    // ------------------------------------------------------------------------------------------------ Hjelpere

    private static void KrevUendret(Kogruppe g, int? forventet)
    {
        if (forventet is { } n && n != g.Forekomster.Count)
        {
            throw new AktorkoKonfliktException($"«{g.Navn}» har {g.Forekomster.Count} forekomster nå, ikke {n} som da du åpnet det. Åpne navnet på "
                                               + "nytt og se over forekomstene før du godkjenner.");
        }
    }

    private static string Normaliser(string? navn) => string.Join(' ', (navn ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Navnet må være en av skrivemåtene i teksten: ingenting skrives om (CLAUDE.md §8, «Statsforvalteren i innlandet»).</summary>
    private static string KrevSkrivemate(Kogruppe g, string? navn)
    {
        var t = string.Join(' ', (navn ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (t.Length == 0 || g.Varianter.SelectMany(v => v.Skrivemater).All(s => s.Tekst != t))
        {
            throw new ArgumentException($"Velg navnet blant skrivemåtene i teksten ({string.Join(", ", g.Varianter.SelectMany(v => v.Skrivemater).Select(s => $"«{s.Tekst}»"))}). "
                                        + "Ingenting er forhåndsvalgt, og navnet skrives ikke om.");
        }
        return t;
    }

    private async Task<List<NavnekandidatEntitet>> LastVentendeAsync(Kogruppe g, CancellationToken ct)
    {
        var ider = g.Forekomster.Select(f => f.NavnekandidatId).ToList();
        var rader = await db.Navnekandidater.Where(k => ider.Contains(k.Id)).ToListAsync(ct);
        if (rader.Count != ider.Count || rader.Any(k => k.Status != "Venter"))
        {
            throw new AktorkoKonfliktException($"En eller flere forekomster av «{g.Navn}» er behandlet siden køen ble lest. Åpne navnet på nytt.");
        }
        return rader.OrderBy(k => k.RettskildeId).ThenBy(k => k.NodeEid, StringComparer.Ordinal).ThenBy(k => k.StartOffset).ToList();
    }

    private static void Godkjenn(IEnumerable<NavnekandidatEntitet> rader, string bruker)
    {
        foreach (var k in rader)
        {
            k.Status = "Godkjent";
            k.AktorkoGruppe = null;
            k.BehandletAv = bruker;
            k.BehandletTidspunkt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>Kjører <paramref name="arbeid"/> i én transaksjon. Feiler noe, rulles alt tilbake og endringssporingen tømmes, så ingen
    /// halvskrevet entitet blir liggende i konteksten.</summary>
    private async Task<T> IEnTransaksjonAsync<T>(Func<Task<T>> arbeid, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var svar = await arbeid();
            if (tx is not null) await tx.CommitAsync(ct);
            return svar;
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<string> TittelAsync(Guid id, CancellationToken ct) =>
        await db.Rettskilder.Where(r => r.Id == id).Select(r => r.Kortnavn ?? r.Tittel).FirstOrDefaultAsync(ct) ?? id.ToString();

    private static string TaggLinje(int antall, int tagget, Kogruppe g)
    {
        var boyninger = g.Varianter.Where(v => v.Regel != "samme_ord").Sum(v => v.Forekomster);
        return $"{tagget} av {antall} forekomster er tagget" + (boyninger > 0 ? $" ({boyninger} er bøyningsformer eller tatt med, og peker på samme navn)" : "")
               + ". Kandidatene er satt til Godkjent.";
    }

    private static string IkkeTaggetForklaring(int antall) =>
        $"{antall} forekomster ble ikke tagget: rettskilden har ikke et ansvarlig departement som kan eie taggen, eller teksten er endret siden "
        + "sveipet. Kandidatene er likevel godkjent.";

    private static string KunnskapsstatusTekst(string? s) => s switch
    {
        Strukturkanter.Tekstfunn => "tekstfunn",
        Strukturkanter.Normalisering => "normalisering",
        Strukturkanter.Uavklart => "uavklart",
        null => "ingen kunnskapsstatus",
        _ => s,
    };

    private static string GodkjenningTekst(string? s) => s switch
    {
        Strukturkanter.FagligVurdert => "faglig vurdert",
        Strukturkanter.Godkjent => "godkjent",
        null => "ikke vurdert",
        _ => s,
    };
}
