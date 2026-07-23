using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>
/// Persisterer et <see cref="KonverteringResultat"/> (fra RegelIde.Kildekonvertering) til databasen
/// (§2 i teknisk design). Idempotent på ELI: importerer aldri samme gjeldende rettskilde to ganger.
/// Løser/oppretter referanse-stubber for eksterne kryssreferanser (§3.1 steg 6) — dette krever
/// nettopp databasetilgang, og var derfor bevisst utenfor den rene konverteringspipelinens scope.
/// </summary>
public sealed class RettskildeImportTjeneste(RegelIdeDbContext db)
{
    // Placeholder inntil ekte autentisering/brukerkontekst finnes i systemet.
    private const string SystemBruker = "system-import";

    public async Task<Guid> ImporterAsync(KonverteringResultat resultat, CancellationToken ct = default)
    {
        var m = resultat.Metadata;
        var eksisterende = await db.Rettskilder
            .FirstOrDefaultAsync(r => r.Eli == m.Eli && r.Entitetsstatus == "gjeldende", ct);

        if (eksisterende is { Importrolle: "primaer" })
        {
            return eksisterende.Id; // allerede importert som primærkilde — ikke dupliser
        }

        Guid rettskildeId;
        if (eksisterende is { Importrolle: "referanse" })
        {
            // Forfremmelse av en tidligere opprettet referanse-stub (§3.1 steg 6) til en fullt importert primærkilde.
            rettskildeId = eksisterende.Id;
            eksisterende.Importrolle = "primaer";
            eksisterende.Doctype = m.Doctype;
            eksisterende.Kildetype = m.Kildetype.ToString();
            eksisterende.Tittel = m.Tittel;
            eksisterende.Kortnavn = m.Kortnavn;
            eksisterende.AknXml = resultat.AknXml;
            eksisterende.Ikrafttredelse = m.Ikrafttredelse;
            eksisterende.KonsolidertDato = m.KonsolidertDato;
            eksisterende.Utgiver = m.Utgiver;
            eksisterende.Status = m.Status;
            eksisterende.SistEndretAv = SystemBruker;
            eksisterende.SistEndretTidspunkt = DateTimeOffset.UtcNow;
            db.Proveniens.Add(NyProveniensrad(rettskildeId, "endret"));
        }
        else
        {
            rettskildeId = Guid.NewGuid();
            db.Rettskilder.Add(new RettskildeEntitet
            {
                Id = rettskildeId,
                Doctype = m.Doctype,
                Kildetype = m.Kildetype.ToString(),
                Importrolle = "primaer",
                Tittel = m.Tittel,
                Kortnavn = m.Kortnavn,
                Eli = m.Eli,
                AknXml = resultat.AknXml,
                Ikrafttredelse = m.Ikrafttredelse,
                KonsolidertDato = m.KonsolidertDato,
                Utgiver = m.Utgiver,
                Status = m.Status,
                OpprettetAv = SystemBruker,
                OpprettetTidspunkt = DateTimeOffset.UtcNow,
            });
            db.Proveniens.Add(NyProveniensrad(rettskildeId, "opprettet"));
        }

        var nodeIdVedEid = resultat.Noder.ToDictionary(n => n.Eid, _ => Guid.NewGuid());
        foreach (var n in resultat.Noder)
        {
            db.RettskildeNoder.Add(new RettskildeNodeEntitet
            {
                Id = nodeIdVedEid[n.Eid],
                RettskildeId = rettskildeId,
                Eid = n.Eid,
                Kildesystem = n.Kildesystem,
                KildeId = n.KildeId,
                OffisiellEli = null,
                ParentNodeId = n.ParentEid is not null ? nodeIdVedEid.GetValueOrDefault(n.ParentEid) : null,
                NodeType = n.NodeType.TilDbVerdi(),
                Nummer = n.Nummer,
                Overskrift = n.Overskrift,
                Tekst = n.Tekst,
                TekstHash = n.TekstHash,
                Sorteringsrekkefolge = n.SorteringsRekkefolge,
            });
        }

        // UNIQUE(fra_node_id, til_rettskilde_id, til_eid) er der nettopp for å "forhindre duplikatimport
        // av samme referanse" (§2-kommentaren) — samme løpetekst kan lenke til samme mål flere ganger
        // innenfor ett og samme ledd (bekreftet reelt i alkoholloven), så vi dedupliserer her istedenfor
        // å la databasen kaste en constraint-feil på noe skjemaet selv sier skal tolereres.
        var alleredeLagtTil = new HashSet<(string FraEid, string TilEid)>();
        foreach (var r in resultat.Referanser)
        {
            if (!nodeIdVedEid.TryGetValue(r.FraNodeEid, out var fraNodeId)) continue;
            if (!alleredeLagtTil.Add((r.FraNodeEid, r.TilEid))) continue;

            var tilRettskildeId = r.ErInternReferanse
                ? rettskildeId
                : await FinnEllerOpprettReferanseStubAsync(r.TilEid, ct);

            db.RettskildeReferanser.Add(new RettskildeReferanseEntitet
            {
                Id = Guid.NewGuid(),
                FraNodeId = fraNodeId,
                TilRettskildeId = tilRettskildeId,
                TilEid = r.TilEid,
            });
        }

        await db.SaveChangesAsync(ct);
        return rettskildeId;
    }

    /// <summary>
    /// Finner en eksisterende rettskilde (primær eller stub) for en ekstern referansemål-ELI, eller
    /// oppretter en referanse-stub (importrolle='referanse', akn_xml=NULL) — §3.1 steg 6.
    /// </summary>
    private async Task<Guid> FinnEllerOpprettReferanseStubAsync(string tilEidEllerEli, CancellationToken ct)
    {
        var dokumentEli = DokumentEliFra(tilEidEllerEli);

        var eksisterende = await db.Rettskilder.FirstOrDefaultAsync(r => r.Eli == dokumentEli && r.Entitetsstatus == "gjeldende", ct);
        if (eksisterende is not null) return eksisterende.Id;

        // Kan allerede være lagt til (men ikke lagret) tidligere i samme import-batch.
        var sporet = db.ChangeTracker.Entries<RettskildeEntitet>()
            .Select(e => e.Entity)
            .FirstOrDefault(r => r.Eli == dokumentEli);
        if (sporet is not null) return sporet.Id;

        var (kildetype, doctype) = TolkKildetypeFraEli(dokumentEli);
        var stubId = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = stubId,
            Doctype = doctype,
            Kildetype = kildetype,
            Importrolle = "referanse",
            Tittel = dokumentEli, // ingen ekte tittel tilgjengelig før stubben forfremmes ved faktisk import
            Eli = dokumentEli,
            AknXml = null,
            Status = "Utkast",
            OpprettetAv = SystemBruker,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        db.Proveniens.Add(NyProveniensrad(stubId, "opprettet"));
        return stubId;
    }

    private static ProveniensEntitet NyProveniensrad(Guid rettskildeId, string handling) => new()
    {
        Id = Guid.NewGuid(),
        EntitetType = "rettskilde",
        EntitetId = rettskildeId,
        EndretAv = SystemBruker,
        Dato = DateTimeOffset.UtcNow,
        Handling = handling,
    };

    /// <summary>Trunkerer en eId (som kan ha et paragraf-/ledd-/punkt-suffiks) til dokumentets egen ELI, ved "/nor".</summary>
    private static string DokumentEliFra(string eidEllerEli)
    {
        var idx = eidEllerEli.IndexOf("/nor", StringComparison.Ordinal);
        return idx < 0 ? eidEllerEli : eidEllerEli[..(idx + 4)];
    }

    private static (string Kildetype, string Doctype) TolkKildetypeFraEli(string eli)
    {
        if (eli.Contains("/eli/lov/", StringComparison.Ordinal)) return ("Lov", "act");
        if (eli.Contains("/eli/forskrift/", StringComparison.Ordinal)) return ("Forskrift", "act");
        throw new NotSupportedException(
            $"Ukjent kildetype i ELI '{eli}' — verken lov eller forskrift. Ingen gjettet fallback (§3.3).");
    }
}
