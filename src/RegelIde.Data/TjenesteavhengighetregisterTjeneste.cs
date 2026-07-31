using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>Én tjenesteavhengighet sett fra én bestemt tjenestes ståsted — <see cref="Retning"/> forteller om denne tjenesten er Fra eller Til, og <see cref="Visningstekst"/> er forhåndsberegnet ut fra samme tabell som docs/03-domenemodell.md §1.5.</summary>
public sealed record TjenesteavhengighetVisning(
    Guid Id, string Rel, string Retning, string Visningstekst,
    Guid MotpartTjenesteId, string MotpartTjenesteTittel, Guid? HendelseId, string? HendelseNavn, string? Beskrivelse);

/// <summary>
/// Tjenesteavhengighetregister (docs/03-domenemodell.md §1.5, docs/13-backlog.md §2.1) — rettede,
/// årsaksforklarte tjeneste-til-tjeneste-koblinger. Ett rettet kant per relasjon (aldri to
/// speilbilde-rader, se domenemodellens presisering "tredje runde") — <see cref="HentForTjenesteAsync"/>
/// beregner riktig visningstekst for BEGGE retninger fra samme rad, ingen duplisert lagring.
/// </summary>
public sealed class TjenesteavhengighetregisterTjeneste(RegelIdeDbContext db)
{
    private static readonly string[] GyldigeRel =
        ["forutsetning_for", "gir_mulighet_til", "utlost_av", "for", "avhengig_av", "input_til"];

    // docs/03-domenemodell.md §1.5 "ett rettet kant per relasjon" — tabellen med Fra-/Til-visningstekst.
    private static readonly Dictionary<string, (string Fra, string Til)> Visningstekster = new()
    {
        ["forutsetning_for"] = ("er forutsetning for {0}", "krever {0}"),
        ["gir_mulighet_til"] = ("gir mulighet til {0}", "forutsetter {0}"),
        ["utlost_av"] = ("kan føre til {0} (via {1})", "kan utløses av {0} (via {1})"),
        ["for"] = ("kommer før {0}", "kommer etter {0}"),
        ["avhengig_av"] = ("{0} er avhengig av denne", "avhengig av {0}"),
        ["input_til"] = ("er input til {0}", "har input fra {0}"),
    };

    public async Task<List<TjenesteavhengighetVisning>> HentForTjenesteAsync(Guid tjenesteId, CancellationToken ct = default)
    {
        var rader = await db.Tjenesteavhengigheter
            .Where(t => t.Entitetsstatus == "gjeldende" && (t.FraTjenesteId == tjenesteId || t.TilTjenesteId == tjenesteId))
            .ToListAsync(ct);
        if (rader.Count == 0) return [];

        var tjenesteIder = rader.SelectMany(r => new[] { r.FraTjenesteId, r.TilTjenesteId }).Distinct().ToList();
        var titler = await db.Tjenester.Where(t => tjenesteIder.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Tittel, ct);
        var hendelseIder = rader.Where(r => r.HendelseId != null).Select(r => r.HendelseId!.Value).Distinct().ToList();
        var hendelseNavn = await db.Hendelser.Where(h => hendelseIder.Contains(h.Id)).ToDictionaryAsync(h => h.Id, h => h.Navn, ct);

        return rader.Select(r =>
        {
            var erFra = r.FraTjenesteId == tjenesteId;
            var motpartId = erFra ? r.TilTjenesteId : r.FraTjenesteId;
            var motpartTittel = titler.GetValueOrDefault(motpartId, "(ukjent tjeneste)");
            var hendelse = r.HendelseId is not null ? hendelseNavn.GetValueOrDefault(r.HendelseId.Value, "(ukjent hendelse)") : null;
            var (fraMal, tilMal) = Visningstekster[r.Rel];
            var visningstekst = string.Format(erFra ? fraMal : tilMal, motpartTittel, hendelse);
            return new TjenesteavhengighetVisning(
                r.Id, r.Rel, erFra ? "fra" : "til", visningstekst, motpartId, motpartTittel, r.HendelseId, hendelse, r.Beskrivelse);
        }).ToList();
    }

    public async Task<TjenesteavhengighetEntitet> OpprettAsync(
        Guid virksomhetId, Guid fraTjenesteId, Guid tilTjenesteId, string rel, Guid? hendelseId, string? beskrivelse,
        string opprettetAv, CancellationToken ct = default)
    {
        if (fraTjenesteId == tilTjenesteId)
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
        if (!await db.Tjenester.AnyAsync(t => t.Id == tilTjenesteId && t.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen tjeneste med id '{tilTjenesteId}'.");
        }
        if (hendelseId is not null && !await db.Hendelser.AnyAsync(h => h.Id == hendelseId && h.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen hendelse med id '{hendelseId}'.");
        }
        if (await db.Tjenesteavhengigheter.AnyAsync(
                t => t.Entitetsstatus == "gjeldende" && t.FraTjenesteId == fraTjenesteId && t.TilTjenesteId == tilTjenesteId && t.Rel == rel, ct))
        {
            throw new ArgumentException("Denne avhengigheten (samme fra/til/rel) finnes allerede.");
        }

        var avhengighet = new TjenesteavhengighetEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            FraTjenesteId = fraTjenesteId,
            TilTjenesteId = tilTjenesteId,
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

    public async Task<bool> SlettAsync(Guid id, CancellationToken ct = default)
    {
        var avhengighet = await db.Tjenesteavhengigheter.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (avhengighet is null) return false;
        db.Tjenesteavhengigheter.Remove(avhengighet);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
