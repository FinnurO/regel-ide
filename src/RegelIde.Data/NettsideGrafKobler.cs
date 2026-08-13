using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>
/// DB-avhengig motstykke til <see cref="NettsideTekstParser"/> (som er, som
/// <see cref="RettskildeImportTjeneste"/>s Lovdata-motstykke <c>LovdataHtmlParser</c>, bevisst
/// DB-fri) — løser de deterministiske kantene fra en <see cref="NettsideDokumentEntitet"/> mot
/// faktiske rader i databasen (docs/15-handbok-dokumentgraf-notat.md §3.2), og skriver dem som
/// <see cref="NettsideLenkeEntitet"/>-rader. Samme arkitektur-todeling som
/// <see cref="RettskildeImportTjeneste.FinnEllerOpprettReferanseStubAsync"/> løser
/// <c>hjemlet_i</c> for håndbøker: parser produserer en kandidat-streng, denne klassen gjør selve
/// oppslaget.
/// </summary>
public sealed class NettsideGrafKobler(RegelIdeDbContext db)
{
    /// <summary>
    /// Lagrer/oppdaterer én <see cref="NettsideDokumentEntitet"/> fra et parseresultat (deduplisert
    /// på <see cref="NettsideDokumentEntitet.KanoniskUrl"/>, §3.4), og skriver dens utgående
    /// <see cref="NettsideLenkeEntitet"/>-kandidater. Løser IKKE <see cref="NettsideLenkeEntitet.TilNettsideDokumentId"/>/
    /// <see cref="NettsideLenkeEntitet.TilRettskildeId"/> her — det gjøres av
    /// <see cref="LoosLenkerAsync"/> i en egen sending, ETTER at alle dokumenter i korpuset er lagret
    /// (en lenke kan peke på et dokument som ennå ikke er importert i samme kjøring — se
    /// <see cref="LoosLenkerAsync"/>s kommentar).
    /// </summary>
    public async Task<Guid> LagreDokumentAsync(NettsideParseResultat resultat, Guid? virksomhetId = null, CancellationToken ct = default)
    {
        var eksisterende = await db.NettsideDokumenter
            .Include(d => d.Stier)
            .SingleOrDefaultAsync(d => d.KanoniskUrl == resultat.Side.KanoniskUrl, ct);

        NettsideDokumentEntitet dokument;
        if (eksisterende is null)
        {
            dokument = new NettsideDokumentEntitet
            {
                Id = Guid.NewGuid(),
                VirksomhetId = virksomhetId,
                KanoniskUrl = resultat.Side.KanoniskUrl,
                Tittel = resultat.Side.Tittel,
                RaaTekst = resultat.Side.RaaTekst,
                InnholdsHash = resultat.Side.InnholdsHash,
                Hentet = DateTimeOffset.UtcNow,
            };
            db.NettsideDokumenter.Add(dokument);
        }
        else
        {
            // Reimport av samme URL (§3.4-dedup) — oppdater innholdet i stedet for å duplisere raden.
            dokument = eksisterende;
            dokument.Tittel = resultat.Side.Tittel;
            dokument.RaaTekst = resultat.Side.RaaTekst;
            dokument.InnholdsHash = resultat.Side.InnholdsHash;
            dokument.Hentet = DateTimeOffset.UtcNow;

            // Rene lenke-rader fra forrige import fjernes og skrives på nytt — enklere og trygt
            // siden lenkene ikke selv har nedstrøms FK-er andre rader avhenger av. Sti-rader
            // (fra NettsideStierAsync) rører vi IKKE her.
            var gamleLenker = await db.NettsideLenker.Where(l => l.FraNettsideDokumentId == dokument.Id).ToListAsync(ct);
            db.NettsideLenker.RemoveRange(gamleLenker);
        }

        foreach (var kandidat in resultat.Lenker)
        {
            db.NettsideLenker.Add(new NettsideLenkeEntitet
            {
                Id = Guid.NewGuid(),
                FraNettsideDokumentId = dokument.Id,
                Type = kandidat.Type == NettsideLenketype.Lovdatalenke ? "lovdatalenke" : "lenker_til",
                RaaHref = kandidat.RaaHref,
                AnkerTekst = kandidat.AnkerTekst,
                TilEidKandidat = kandidat.TilEidKandidat,
            });
        }

        await db.SaveChangesAsync(ct);
        return dokument.Id;
    }

    /// <summary>
    /// §3.4: lagre ALLE stier et dokument opptrer under, som separate rader — idempotent
    /// (finnes raden allerede, gjøres ingenting).
    /// </summary>
    public async Task LeggTilStiAsync(Guid nettsideDokumentId, string sti, string stiType, CancellationToken ct = default)
    {
        var finnes = await db.NettsideStier.AnyAsync(
            s => s.NettsideDokumentId == nettsideDokumentId && s.StiType == stiType && s.Sti == sti, ct);
        if (finnes) return;

        db.NettsideStier.Add(new NettsideStiEntitet
        {
            Id = Guid.NewGuid(),
            NettsideDokumentId = nettsideDokumentId,
            Sti = sti,
            StiType = stiType,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Løser <see cref="NettsideLenkeEntitet.TilNettsideDokumentId"/> (intern lenke — matcher
    /// <see cref="NettsideLenkeEntitet.RaaHref"/>, absolutt ELLER relativ, mot en kjent
    /// <see cref="NettsideDokumentEntitet.KanoniskUrl"/>) og <see cref="NettsideLenkeEntitet.TilRettskildeId"/>
    /// (<c>lovdatalenke</c> — matcher <see cref="NettsideLenkeEntitet.TilEidKandidat"/> mot
    /// <see cref="RettskildeEntitet.Eli"/>, ELLER matcher en <c>lenker_til</c>-kandidats
    /// <see cref="NettsideLenkeEntitet.RaaHref"/> mot en importert håndboks eksakte
    /// <see cref="RettskildeEntitet.Url"/> — "PDF-omtale"-koblingen oppgaven ba om). Kjøres som EGET
    /// steg ETTER at alle dokumenter/rettskilder er lagret, bevisst — en lenke kan sikte på et
    /// dokument importert i en SENERE runde (samme "løs det du kan, dropp stille resten"-prinsipp
    /// som <c>HandbokTekstParser.TrekkUtReferanser</c> bruker for uløste kryssreferanser).
    /// </summary>
    public async Task<int> LoosLenkerAsync(CancellationToken ct = default)
    {
        var ulostAntall = 0;

        var lenker = await db.NettsideLenker
            .Where(l => l.TilNettsideDokumentId == null && l.TilRettskildeId == null)
            .ToListAsync(ct);

        var dokumentUrler = await db.NettsideDokumenter.Select(d => new { d.Id, d.KanoniskUrl }).ToListAsync(ct);
        var rettskilderMedEli = await db.Rettskilder.Where(r => r.Eli != null).Select(r => new { r.Id, r.Eli }).ToListAsync(ct);
        var rettskilderMedUrl = await db.Rettskilder.Where(r => r.Url != null).Select(r => new { r.Id, r.Url }).ToListAsync(ct);

        foreach (var lenke in lenker)
        {
            var internMatch = dokumentUrler.FirstOrDefault(d => ErSammeUrl(lenke.RaaHref, d.KanoniskUrl));
            if (internMatch is not null)
            {
                lenke.TilNettsideDokumentId = internMatch.Id;
                continue;
            }

            if (lenke.Type == "lovdatalenke" && lenke.TilEidKandidat is not null)
            {
                var rettskildeMatch = rettskilderMedEli.FirstOrDefault(r => r.Eli == lenke.TilEidKandidat);
                if (rettskildeMatch is not null)
                {
                    lenke.TilRettskildeId = rettskildeMatch.Id;
                    continue;
                }
            }

            // "PDF-omtale"-koblingen: en ordinær lenke (typisk til /api/rest/filer/...) som peker
            // eksakt på Url-feltet til en allerede importert håndbok/forskrift (§2 Lag 1).
            var pdfMatch = rettskilderMedUrl.FirstOrDefault(r => ErSammeUrl(lenke.RaaHref, r.Url!));
            if (pdfMatch is not null)
            {
                lenke.TilRettskildeId = pdfMatch.Id;
                continue;
            }

            ulostAntall++;
        }

        await db.SaveChangesAsync(ct);
        return ulostAntall;
    }

    /// <summary>
    /// Sammenligner en (potensielt relativ) href mot en kjent absolutt/kanonisk URL — tolerant på
    /// skjema (http/https) og trailing slash, IKKE på vertsnavn/sti forøvrig (ingen gjettet
    /// normalisering utover dette).
    /// </summary>
    private static bool ErSammeUrl(string href, string kanoniskUrl)
    {
        var a = NormaliserSti(href);
        var b = NormaliserSti(kanoniskUrl);
        return a.Equals(b, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormaliserSti(string url)
    {
        var u = url.Trim();
        if (u.StartsWith("https://www.bergen.kommune.no", StringComparison.OrdinalIgnoreCase))
            u = u["https://www.bergen.kommune.no".Length..];
        else if (u.StartsWith("http://www.bergen.kommune.no", StringComparison.OrdinalIgnoreCase))
            u = u["http://www.bergen.kommune.no".Length..];
        return u.TrimEnd('/');
    }
}
