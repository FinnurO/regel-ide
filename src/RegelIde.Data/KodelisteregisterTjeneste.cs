using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Kodelisteregister / verdidomene (docs/03-domenemodell.md §1.4) — byggesteg 2. Tre typer (§0.1):
/// juridisk/teknisk krever <see cref="KodelisteEntitet.VirksomhetId"/> (eget arbeidsprodukt);
/// ekstern-referanse er delt (ingen virksomhet, ingen dupliseringen av en autoritativ kilde) og har
/// ikke noe publiseringssteg (§3.1) — forblir alltid 'publisert' (ekvivalenten til "alltid gjeldende
/// så lenge kilden er det"), aldri gjennom utkast/validering.
/// </summary>
public sealed class KodelisteregisterTjeneste(RegelIdeDbContext db)
{
    private static readonly string[] GyldigeTyper = ["juridisk", "teknisk", "ekstern-referanse"];
    private static readonly string[] GyldigeStatuser =
        ["utkast", "under_revisjon", "validert", "publisert", "tilbaketrukket", "arkivert"];

    public Task<List<KodelisteEntitet>> AlleAsync(CancellationToken ct = default) =>
        db.Kodelister.Include(k => k.Koder).Where(k => k.Entitetsstatus == "gjeldende").OrderBy(k => k.Kode).ToListAsync(ct);

    public Task<KodelisteEntitet?> FinnAsync(Guid id, CancellationToken ct = default) =>
        db.Kodelister.Include(k => k.Koder).FirstOrDefaultAsync(k => k.Id == id && k.Entitetsstatus == "gjeldende", ct);

    public async Task<KodelisteEntitet> OpprettAsync(
        Guid? virksomhetId, string kode, string navn, string type, string? juridiskGrunnlagEid,
        string? eksternKildeUri, string? eksternKildeVersjon, string opprettetAv, CancellationToken ct = default)
    {
        await ValiderAsync(virksomhetId, kode, navn, type, juridiskGrunnlagEid, eksternKildeUri, ct);

        var kodeliste = new KodelisteEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            Kode = kode,
            Navn = navn,
            Type = type,
            JuridiskGrunnlagEid = juridiskGrunnlagEid,
            EksternKildeUri = eksternKildeUri,
            EksternKildeVersjon = eksternKildeVersjon,
            // Ekstern-referanse har ikke et publiseringssteg (§3.1) — "alltid gjeldende" modellert som
            // at Status starter og forblir 'publisert', se SettStatusAsync under.
            Status = type == "ekstern-referanse" ? "publisert" : "utkast",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Kodelister.Add(kodeliste);
        db.Proveniens.Add(ProveniensHjelper.NyRad("kodeliste", kodeliste.Id, virksomhetId, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return kodeliste;
    }

    public async Task<KodelisteKodeEntitet?> LeggTilKodeAsync(
        Guid kodelisteId, string kode, string term, string? definisjon, DateOnly? gyldigFra, DateOnly? gyldigTil,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(kode) || string.IsNullOrWhiteSpace(term))
        {
            throw new ArgumentException("Kode og term kan ikke være tomme. Ingen gjettet fallback.");
        }
        if (!await db.Kodelister.AnyAsync(k => k.Id == kodelisteId && k.Entitetsstatus == "gjeldende", ct))
        {
            return null;
        }
        if (await db.KodelisteKoder.AnyAsync(k => k.KodelisteId == kodelisteId && k.Kode == kode, ct))
        {
            throw new ArgumentException($"Koden '{kode}' finnes allerede i denne kodelisten.");
        }

        var kodeRad = new KodelisteKodeEntitet
        {
            Id = Guid.NewGuid(), KodelisteId = kodelisteId, Kode = kode, Term = term,
            Definisjon = definisjon, GyldigFra = gyldigFra, GyldigTil = gyldigTil,
        };
        db.KodelisteKoder.Add(kodeRad);
        await db.SaveChangesAsync(ct);
        return kodeRad;
    }

    public async Task<bool> FjernKodeAsync(Guid kodeId, CancellationToken ct = default)
    {
        var kodeRad = await db.KodelisteKoder.FirstOrDefaultAsync(k => k.Id == kodeId, ct);
        if (kodeRad is null) return false;
        db.KodelisteKoder.Remove(kodeRad);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<KodelisteEntitet?> SettStatusAsync(Guid id, string nyStatus, string endretAv, CancellationToken ct = default)
    {
        if (!GyldigeStatuser.Contains(nyStatus))
        {
            throw new ArgumentException($"Ukjent status '{nyStatus}'. Gyldige verdier: {string.Join(", ", GyldigeStatuser)}.");
        }

        var kodeliste = await db.Kodelister.FirstOrDefaultAsync(k => k.Id == id && k.Entitetsstatus == "gjeldende", ct);
        if (kodeliste is null) return null;
        if (kodeliste.Type == "ekstern-referanse")
        {
            throw new ArgumentException("Eksterne autoritative kodelister har ikke et publiseringssteg (§3.1) — alltid 'publisert'.");
        }

        kodeliste.Status = nyStatus;
        kodeliste.SistEndretAv = endretAv;
        kodeliste.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        db.Proveniens.Add(ProveniensHjelper.NyRad("kodeliste", kodeliste.Id, kodeliste.VirksomhetId, nyStatus == "publisert" ? "publisert" : "endret", endretAv));
        await db.SaveChangesAsync(ct);
        return kodeliste;
    }

    private async Task ValiderAsync(
        Guid? virksomhetId, string kode, string navn, string type, string? juridiskGrunnlagEid,
        string? eksternKildeUri, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(kode) || string.IsNullOrWhiteSpace(navn))
        {
            throw new ArgumentException("Kode og navn kan ikke være tomme. Ingen gjettet fallback.");
        }
        if (!GyldigeTyper.Contains(type))
        {
            throw new ArgumentException($"Ukjent type '{type}'. Gyldige verdier: {string.Join(", ", GyldigeTyper)}.");
        }
        if (type == "ekstern-referanse")
        {
            if (virksomhetId is not null)
            {
                throw new ArgumentException("Ekstern-referanse-kodelister er delt/nasjonale og kan ikke ha en virksomhet (§0.1).");
            }
            if (string.IsNullOrWhiteSpace(eksternKildeUri))
            {
                throw new ArgumentException("Ekstern-referanse-kodelister krever eksternKildeUri.");
            }
        }
        else
        {
            if (virksomhetId is null)
            {
                throw new ArgumentException($"Type '{type}' krever en virksomhet (§0.1) — kun ekstern-referanse er delt/uten virksomhet.");
            }
            if (eksternKildeUri is not null)
            {
                throw new ArgumentException("eksternKildeUri er kun gyldig for type 'ekstern-referanse'.");
            }
        }
        if (type != "juridisk" && juridiskGrunnlagEid is not null)
        {
            throw new ArgumentException("juridiskGrunnlagEid er kun gyldig for type 'juridisk'.");
        }
        if (juridiskGrunnlagEid is not null && !await db.RettskildeNoder.AnyAsync(n => n.Eid == juridiskGrunnlagEid, ct))
        {
            throw new ArgumentException($"Fant ingen rettskilde-node med eId '{juridiskGrunnlagEid}'. Ingen gjettet fallback.");
        }
        if (await db.Kodelister.AnyAsync(k => k.Kode == kode && k.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Koden '{kode}' er allerede i bruk.");
        }
    }
}
