using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Én tjenesteavhengighet sett fra én bestemt tjenestes ståsted — <see cref="Retning"/> forteller om denne
/// tjenesten er Fra eller Til, og <see cref="Visningstekst"/> er forhåndsberegnet ut fra samme tabell som
/// docs/03-domenemodell.md §1.5.
/// <para>
/// (2026-08-19, feature/tjenesteavhengighet-ekstern-referanse) Motparten er ett av to: en ekte tjeneste
/// (<see cref="MotpartTjenesteId"/> satt, <see cref="MotpartOrganisasjonsnummer"/> null) eller en
/// <see cref="EksternTjenestereferanseEntitet"/>-plassholder (<see cref="MotpartTjenesteId"/> null,
/// <see cref="MotpartOrganisasjonsnummer"/> satt). <see cref="MotpartNavn"/> er ALLTID populert uansett
/// hvilket av de to — klienten kan rendre denne uten selv å måtte vite hvilket tilfelle det er, og bruker
/// null-sjekken på <see cref="MotpartTjenesteId"/> kun til å avgjøre om en <c>/tjenester/:id</c>-lenke
/// gir mening (aldri for en ekstern referanse — den har ingen ekte Tjeneste-rad å navigere til).
/// </para>
/// </summary>
public sealed record TjenesteavhengighetVisning(
    Guid Id, string Rel, string Retning, string Visningstekst,
    Guid? MotpartTjenesteId, string? MotpartOrganisasjonsnummer, string MotpartNavn, string? MotpartUrl,
    Guid? HendelseId, string? HendelseNavn, string? Beskrivelse);

/// <summary>
/// Tjenesteavhengighetregister (docs/03-domenemodell.md §1.5, docs/13-backlog.md §2.1) — rettede,
/// årsaksforklarte tjeneste-til-tjeneste-koblinger. Ett rettet kant per relasjon (aldri to
/// speilbilde-rader, se domenemodellens presisering "tredje runde") — <see cref="HentForTjenesteAsync"/>
/// beregner riktig visningstekst for BEGGE retninger fra samme rad, ingen duplisert lagring.
/// </summary>
public sealed class TjenesteavhengighetregisterTjeneste(RegelIdeDbContext db)
{
    // internal (ikke private) siden TjenesteforslagTjeneste (byggesteg 5 runde 4) gjenbruker denne
    // listen i sin system-instruks til KI-agenten, i stedet for å duplisere den og risikere drift.
    internal static readonly string[] GyldigeRel =
        ["forutsetning_for", "gir_mulighet_til", "utlost_av", "for", "avhengig_av", "input_til", "har_del", "kan_miste"];

    // docs/03-domenemodell.md §1.5 "ett rettet kant per relasjon" — tabellen med Fra-/Til-visningstekst.
    // "har_del" lagt til byggesteg 5 runde 4 — dekker dct:hasPart-siden av det CPSV-AP-NO-konseptet
    // docs/14-byggesteg5-teknisk-design.md §7 dokumenterte som utsatt i runde 3 (kun selve Rel-verdien,
    // ingen egen typed komposisjons-/rekkefølge-struktur — se docs/13-backlog.md for hva som fortsatt
    // er åpent).
    private static readonly Dictionary<string, (string Fra, string Til)> Visningstekster = new()
    {
        ["forutsetning_for"] = ("er forutsetning for {0}", "krever {0}"),
        ["gir_mulighet_til"] = ("gir mulighet til {0}", "forutsetter {0}"),
        ["utlost_av"] = ("kan føre til {0} (via {1})", "kan utløses av {0} (via {1})"),
        ["for"] = ("kommer før {0}", "kommer etter {0}"),
        ["avhengig_av"] = ("{0} er avhengig av denne", "avhengig av {0}"),
        ["input_til"] = ("er input til {0}", "har input fra {0}"),
        ["har_del"] = ("har del {0}", "er del av {0}"),
        // Lagt til 2026-08-20 (Rettighet/Handling-modellrunden) — for avhengigheter der motparten ikke
        // er en forutsetning, men en risiko: rettigheten kan gå tapt som følge av at motparten
        // inntreffer (f.eks. Serveringsbevilling "kan_miste" ved Kunngjøring av konkurs).
        ["kan_miste"] = ("kan miste retten pga. {0}", "kan utløse tap av {0}"),
    };

    public async Task<List<TjenesteavhengighetVisning>> HentForTjenesteAsync(Guid tjenesteId, CancellationToken ct = default)
    {
        var rader = await db.Tjenesteavhengigheter
            .Where(t => t.Entitetsstatus == "gjeldende" && (t.FraTjenesteId == tjenesteId || t.TilTjenesteId == tjenesteId))
            .ToListAsync(ct);
        if (rader.Count == 0) return [];

        var tjenesteIder = rader.SelectMany(r => new[] { r.FraTjenesteId, r.TilTjenesteId })
            .Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        var titler = await db.Tjenester.Where(t => tjenesteIder.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Tittel, ct);

        var eksternIder = rader.Where(r => r.TilEksternReferanseId is not null).Select(r => r.TilEksternReferanseId!.Value).Distinct().ToList();
        var eksterne = await db.EksterneTjenestereferanser.Where(e => eksternIder.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e, ct);

        var hendelseIder = rader.Where(r => r.HendelseId != null).Select(r => r.HendelseId!.Value).Distinct().ToList();
        var hendelseNavn = await db.Hendelser.Where(h => hendelseIder.Contains(h.Id)).ToDictionaryAsync(h => h.Id, h => h.Navn, ct);

        return rader.Select(r =>
        {
            var erFra = r.FraTjenesteId == tjenesteId;

            // FraTjenesteId er ALLTID en ekte tjeneste (se TjenesteavhengighetEntitets klassekommentar) —
            // kun Til-siden kan være en ekstern plassholder. Når vi ser fra Fra-siden må vi derfor sjekke
            // hvilket av de to Til-feltene som er satt; når vi ser fra Til-siden er motparten alltid FraTjenesteId.
            Guid? motpartTjenesteId;
            string? motpartOrgnr;
            string motpartNavn;
            string? motpartUrl;
            if (erFra)
            {
                if (r.TilTjenesteId is not null)
                {
                    motpartTjenesteId = r.TilTjenesteId;
                    motpartOrgnr = null;
                    motpartUrl = null;
                    motpartNavn = titler.GetValueOrDefault(r.TilTjenesteId.Value, "(ukjent tjeneste)");
                }
                else
                {
                    var ekstern = eksterne.GetValueOrDefault(r.TilEksternReferanseId!.Value);
                    motpartTjenesteId = null;
                    motpartOrgnr = ekstern?.Organisasjonsnummer;
                    motpartUrl = ekstern?.Url;
                    motpartNavn = ekstern?.Navn ?? "(ukjent ekstern referanse)";
                }
            }
            else
            {
                motpartTjenesteId = r.FraTjenesteId;
                motpartOrgnr = null;
                motpartUrl = null;
                motpartNavn = titler.GetValueOrDefault(r.FraTjenesteId, "(ukjent tjeneste)");
            }

            var hendelse = r.HendelseId is not null ? hendelseNavn.GetValueOrDefault(r.HendelseId.Value, "(ukjent hendelse)") : null;
            var (fraMal, tilMal) = Visningstekster[r.Rel];
            var visningstekst = string.Format(erFra ? fraMal : tilMal, motpartNavn, hendelse);
            return new TjenesteavhengighetVisning(
                r.Id, r.Rel, erFra ? "fra" : "til", visningstekst,
                motpartTjenesteId, motpartOrgnr, motpartNavn, motpartUrl, r.HendelseId, hendelse, r.Beskrivelse);
        }).ToList();
    }

    /// <summary>
    /// Oppretter en rettet kant FRA <paramref name="fraTjenesteId"/> (alltid kallerens egen, ekte tjeneste)
    /// TIL ETT av: en ekte tjeneste (<paramref name="tilTjenesteId"/>, typisk funnet via cross-tenant-søket)
    /// eller en ekstern plassholder (<paramref name="tilOrganisasjonsnummer"/>+<paramref name="tilNavn"/>,
    /// valgfritt <paramref name="tilUrl"/>). NØYAKTIG ett av de to målene må oppgis — se
    /// <see cref="TjenesteavhengighetEntitet"/>s klassekommentar. For det eksterne tilfellet matches en
    /// EKSISTERENDE <see cref="EksternTjenestereferanseEntitet"/> på (organisasjonsnummer, navn) før en ny
    /// opprettes (2026-08-19) — gjentatt referering av samme eksterne tjeneste fra flere kanter skal ikke
    /// spamme nye plassholder-rader.
    /// </summary>
    public async Task<TjenesteavhengighetEntitet> OpprettAsync(
        Guid virksomhetId, Guid fraTjenesteId, Guid? tilTjenesteId, string rel, Guid? hendelseId, string? beskrivelse,
        string opprettetAv, string? tilOrganisasjonsnummer = null, string? tilNavn = null, string? tilUrl = null,
        CancellationToken ct = default)
    {
        var harEksternMal = !string.IsNullOrWhiteSpace(tilOrganisasjonsnummer) || !string.IsNullOrWhiteSpace(tilNavn);
        if (tilTjenesteId is not null && harEksternMal)
        {
            throw new ArgumentException("Oppgi enten TilTjenesteId eller organisasjonsnummer+navn til en ekstern referanse — ikke begge.");
        }
        if (tilTjenesteId is null && !harEksternMal)
        {
            throw new ArgumentException("Mål for avhengigheten mangler. Oppgi enten TilTjenesteId eller organisasjonsnummer+navn til en ekstern referanse. Ingen gjettet fallback.");
        }
        if (harEksternMal && (string.IsNullOrWhiteSpace(tilOrganisasjonsnummer) || string.IsNullOrWhiteSpace(tilNavn)))
        {
            throw new ArgumentException("En ekstern referanse krever både organisasjonsnummer og navn. Ingen gjettet fallback.");
        }
        if (tilTjenesteId is not null && fraTjenesteId == tilTjenesteId)
        {
            throw new ArgumentException("En tjeneste kan ikke ha en avhengighet til seg selv.");
        }
        if (!GyldigeRel.Contains(rel))
        {
            throw new ArgumentException($"Ukjent rel '{rel}'. Gyldige verdier: {string.Join(", ", GyldigeRel)}.");
        }
        if (hendelseId is not null && rel != "utlost_av")
        {
            throw new ArgumentException("HendelseId er kun gyldig når rel er 'utlost_av'.");
        }
        if (!await db.Tjenester.AnyAsync(t => t.Id == fraTjenesteId && t.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen tjeneste med id '{fraTjenesteId}'.");
        }
        if (tilTjenesteId is not null && !await db.Tjenester.AnyAsync(t => t.Id == tilTjenesteId && t.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen tjeneste med id '{tilTjenesteId}'.");
        }
        if (hendelseId is not null && !await db.Hendelser.AnyAsync(h => h.Id == hendelseId && h.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen hendelse med id '{hendelseId}'.");
        }

        Guid? tilEksternReferanseId = null;
        if (harEksternMal)
        {
            var eksisterende = await db.EksterneTjenestereferanser.FirstOrDefaultAsync(
                e => e.Organisasjonsnummer == tilOrganisasjonsnummer && e.Navn == tilNavn, ct);
            if (eksisterende is not null)
            {
                tilEksternReferanseId = eksisterende.Id;
            }
            else
            {
                var nyReferanse = new EksternTjenestereferanseEntitet
                {
                    Id = Guid.NewGuid(),
                    Organisasjonsnummer = tilOrganisasjonsnummer!,
                    Navn = tilNavn!,
                    Url = tilUrl,
                    OpprettetAv = opprettetAv,
                    OpprettetTidspunkt = DateTimeOffset.UtcNow,
                };
                db.EksterneTjenestereferanser.Add(nyReferanse);
                tilEksternReferanseId = nyReferanse.Id;
            }
        }

        var duplikatFinnes = tilTjenesteId is not null
            ? await db.Tjenesteavhengigheter.AnyAsync(
                t => t.Entitetsstatus == "gjeldende" && t.FraTjenesteId == fraTjenesteId && t.TilTjenesteId == tilTjenesteId && t.Rel == rel, ct)
            : await db.Tjenesteavhengigheter.AnyAsync(
                t => t.Entitetsstatus == "gjeldende" && t.FraTjenesteId == fraTjenesteId && t.TilEksternReferanseId == tilEksternReferanseId && t.Rel == rel, ct);
        if (duplikatFinnes)
        {
            throw new ArgumentException("Denne avhengigheten (samme fra/til/rel) finnes allerede.");
        }
        if (tilTjenesteId is not null && await LukkerSykelAsync(fraTjenesteId, tilTjenesteId.Value, ct))
        {
            throw new ArgumentException(
                $"Denne avhengigheten ville lukket en sykel — '{tilTjenesteId}' kan allerede (transitivt) nå '{fraTjenesteId}'.");
        }

        var avhengighet = new TjenesteavhengighetEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            FraTjenesteId = fraTjenesteId,
            TilTjenesteId = tilTjenesteId,
            TilEksternReferanseId = tilEksternReferanseId,
            Rel = rel,
            HendelseId = hendelseId,
            Beskrivelse = beskrivelse,
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Tjenesteavhengigheter.Add(avhengighet);
        db.Proveniens.Add(ProveniensHjelper.NyRad("tjenesteavhengighet", avhengighet.Id, virksomhetId, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return avhengighet;
    }

    /// <summary>
    /// BFS fra <paramref name="tilTjenesteId"/> over eksisterende, gjeldende
    /// <see cref="TjenesteavhengighetEntitet"/>-kanter — sant hvis <paramref name="fraTjenesteId"/> er
    /// nåbar, dvs. at en NY kant fraTjenesteId→tilTjenesteId ville lukket en sykel. Byggesteg 5 runde 4
    /// — fantes ikke tidligere i det hele tatt (kun selvreferanse+duplikat ble sjekket), i motsetning
    /// til <c>VilkarstreGrafHjelper.FinnStiAsync</c> som allerede gjør dette for vilkårstreet. Egen,
    /// enklere BFS her siden dette kun spenner over ÉN kant-type (denne tabellen), ikke flere
    /// node-typer som vilkårstreet.
    /// <para>
    /// (2026-08-19) Kalles KUN når <c>tilTjenesteId</c> er en ekte tjeneste — en ekstern plassholder er
    /// et blindspor uten utgående kanter (<see cref="TjenesteEntitet.Id"/>-basert BFS-en under kan aldri
    /// starte der), og kan derfor per definisjon ikke lukke en sykel.
    /// </para>
    /// </summary>
    private async Task<bool> LukkerSykelAsync(Guid fraTjenesteId, Guid tilTjenesteId, CancellationToken ct)
    {
        var besokt = new HashSet<Guid> { tilTjenesteId };
        var ko = new Queue<Guid>();
        ko.Enqueue(tilTjenesteId);
        while (ko.Count > 0)
        {
            var gjeldende = ko.Dequeue();
            var naboer = await db.Tjenesteavhengigheter
                .Where(t => t.Entitetsstatus == "gjeldende" && t.FraTjenesteId == gjeldende && t.TilTjenesteId != null)
                .Select(t => t.TilTjenesteId!.Value)
                .ToListAsync(ct);
            foreach (var nabo in naboer)
            {
                if (nabo == fraTjenesteId) return true;
                if (besokt.Add(nabo)) ko.Enqueue(nabo);
            }
        }
        return false;
    }

    public async Task<bool> SlettAsync(Guid id, CancellationToken ct = default)
    {
        var avhengighet = await db.Tjenesteavhengigheter.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (avhengighet is null) return false;
        db.Tjenesteavhengigheter.Remove(avhengighet);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
