using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>Resultat av å opprette/redigere en kommentar-seksjon: node + dens 1:1-metadata.</summary>
public sealed record HandbokNodeResultat(RettskildeNodeEntitet Node, HandbokKommentarMetadataEntitet Metadata);

/// <summary>
/// Forfatterflyt for håndbok/rundskriv (docs/03-domenemodell.md §1.1.1, docs/08-byggesteg1-teknisk-design.md
/// §2.2). I motsetning til Lov/Forskrift (parset via <see cref="LovdataKonverterer"/> + persistert av
/// <see cref="RettskildeImportTjeneste"/>, formet rundt et ferdig <c>KonverteringResultat</c>) har en
/// håndbok ingen importpipeline — den forfattes direkte i verktøyet, derav egen tjeneste.
///
/// Samme stil som <see cref="TekstTaggTjeneste"/>: primary-constructor DI, ingen abstraksjonslag,
/// <see cref="ArgumentException"/> for domenevalidering ("ingen gjettet fallback", §3.3), dual-write av
/// domenerad + proveniensrad i samme <c>SaveChangesAsync</c>.
/// </summary>
public sealed class HandbokForfatterTjeneste(RegelIdeDbContext db)
{
    private static readonly string[] GyldigeDokumenttyper = ["kommentar", "retningslinje", "instruks", "handbok"];
    private static readonly string[] GyldigeFesteNivaer = ["kapittel", "bestemmelse", "ledd", "bokstav"];

    /// <summary>
    /// Oppretter selve håndbok-rettskilden (`kildetype='Rundskriv'`, `doctype='doc'`, `status='Utkast'`).
    /// </summary>
    public async Task<RettskildeEntitet> OpprettHandbokAsync(
        string tittel, Guid virksomhetId, string opprettetAv, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tittel))
        {
            throw new ArgumentException("Tittel kan ikke være tom. Ingen gjettet fallback.");
        }

        var handbok = new RettskildeEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            Doctype = "doc",
            Kildetype = "Rundskriv",
            Importrolle = "primaer",
            Tittel = tittel,
            AknXml = MinimalAknPlassholder(tittel),
            // Bevisst "Gjeldende", ikke "Utkast" som teknisk design §2.2s skisse antyder: RettskildeEntitet
            // sin "Utkast"-status (§3.2) er en offentlig-synlighet-sperre for IKKE-verifisert PARSET
            // innhold (RettskildeRepository skjuler Utkast helt, også for eieren selv — det finnes ingen
            // "vis meg mine egne kladder"-vei siden lesesiden bevisst er uautentisert/åpen, §0.1). En
            // håndbok forfattes derimot direkte, seksjon for seksjon — det er nettopp
            // HandbokKommentarMetadataEntitet.Status (under_arbeid → publisert) som styrer om INNHOLDET
            // er klart, ikke rettskilde-dokumentets egen status. "Utkast" her ville gjort håndboken
            // usynlig for forfatteren selv gjennom akkurat de gjenopprettede lese-endepunktene UI-et bruker.
            Status = "Gjeldende",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Rettskilder.Add(handbok);
        db.Proveniens.Add(ProveniensHjelper.NyRad("rettskilde", handbok.Id, virksomhetId, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return handbok;
    }

    /// <summary>
    /// Oppretter en kapittel-/underinndelingsnode. Ingen <see cref="HandbokKommentarMetadataEntitet"/> —
    /// kun kommentar-seksjoner (<see cref="OpprettKommentarNodeAsync"/>) har metadata.
    /// </summary>
    public async Task<RettskildeNodeEntitet> OpprettKapittelNodeAsync(
        Guid rettskildeId, Guid? parentNodeId, string nummer, string? overskrift, string opprettetAv, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nummer))
        {
            throw new ArgumentException("Nummer kan ikke være tomt. Ingen gjettet fallback.");
        }

        var parent = parentNodeId is null ? null : await HentGjeldendeNodeAsync(parentNodeId.Value, ct);
        if (parentNodeId is not null && parent is null)
        {
            throw new ArgumentException($"Foreldrenode '{parentNodeId}' finnes ikke eller er ikke gjeldende.");
        }

        var node = new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(),
            RettskildeId = rettskildeId,
            Eid = LagEid(parent?.Eid, nummer),
            Kildesystem = "regel-ide",
            KildeId = nummer,
            ParentNodeId = parentNodeId,
            NodeType = parent is null ? "kapittel" : "underinndeling",
            Nummer = nummer,
            Overskrift = overskrift,
            Sorteringsrekkefolge = await NesteSorteringAsync(rettskildeId, parentNodeId, ct),
        };
        db.RettskildeNoder.Add(node);
        db.Proveniens.Add(ProveniensHjelper.NyRad("rettskilde_node", node.Id, null, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return node;
    }

    /// <summary>
    /// Oppretter en kommentar-seksjon (versjon 1). <paramref name="tekstHtml"/> saneres til det
    /// begrensede markup-settet server-side (§1.1.1 "Redigeringsflate") før lagring.
    /// </summary>
    public async Task<HandbokNodeResultat> OpprettKommentarNodeAsync(
        Guid rettskildeId, Guid parentNodeId, string nummer, string? overskrift, string tekstHtml,
        string dokumenttype, string festeNiva, IReadOnlyList<string>? marginord, string opprettetAv,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nummer))
        {
            throw new ArgumentException("Nummer kan ikke være tomt. Ingen gjettet fallback.");
        }
        ValiderDokumenttype(dokumenttype);
        ValiderFesteNiva(festeNiva);

        var parent = await HentGjeldendeNodeAsync(parentNodeId, ct);
        if (parent is null)
        {
            throw new ArgumentException($"Foreldrenode '{parentNodeId}' finnes ikke eller er ikke gjeldende.");
        }

        var sanertTekst = KommentarTekstSanering.Saner(tekstHtml);
        var node = new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(),
            RettskildeId = rettskildeId,
            Eid = LagEid(parent.Eid, nummer),
            Kildesystem = "regel-ide",
            KildeId = nummer,
            ParentNodeId = parentNodeId,
            NodeType = "ledd", // kommentar-seksjonen er alltid en bladtekst-bærende node i håndbokens eget tre
            Nummer = nummer,
            Overskrift = overskrift,
            Tekst = sanertTekst,
            TekstHash = LovdataIdentifikatorer.BeregnTekstHash(sanertTekst),
            Sorteringsrekkefolge = await NesteSorteringAsync(rettskildeId, parentNodeId, ct),
        };
        var metadata = new HandbokKommentarMetadataEntitet
        {
            NodeId = node.Id,
            Dokumenttype = dokumenttype,
            Bindende = UtledBindende(dokumenttype),
            FesteNiva = festeNiva,
            Status = "under_arbeid",
            SistFagligEndret = DateOnly.FromDateTime(DateTime.UtcNow),
            Marginord = marginord?.ToList() ?? [],
        };
        db.RettskildeNoder.Add(node);
        db.HandbokKommentarMetadata.Add(metadata);
        db.Proveniens.Add(ProveniensHjelper.NyRad("rettskilde_node", node.Id, null, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return new HandbokNodeResultat(node, metadata);
    }

    /// <summary>
    /// Redigerer en EKSISTERENDE kommentar-seksjon. Oppretter alltid en NY node-rad (`versjon++`,
    /// `erstatter_node_id` peker til forrige) og setter forrige rads `entitetsstatus='erstattet'` —
    /// aldri en in-place-oppdatering av selve innholdet (§2.1).
    /// </summary>
    public async Task<HandbokNodeResultat> RedigerKommentarNodeAsync(
        Guid nodeId, string tekstHtml, string? overskrift, string dokumenttype, string festeNiva,
        IReadOnlyList<string>? marginord, string endretAv, CancellationToken ct = default)
    {
        ValiderDokumenttype(dokumenttype);
        ValiderFesteNiva(festeNiva);

        var forrige = await db.RettskildeNoder.FirstOrDefaultAsync(n => n.Id == nodeId && n.Entitetsstatus == "gjeldende", ct);
        if (forrige is null)
        {
            throw new ArgumentException($"Node '{nodeId}' finnes ikke eller er ikke gjeldende.");
        }
        var forrigeMetadata = await db.HandbokKommentarMetadata.FirstOrDefaultAsync(m => m.NodeId == nodeId, ct);
        if (forrigeMetadata is null)
        {
            throw new ArgumentException($"Node '{nodeId}' er ikke en håndbok-kommentarseksjon.");
        }

        var sanertTekst = KommentarTekstSanering.Saner(tekstHtml);
        var nyNode = new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(),
            RettskildeId = forrige.RettskildeId,
            Eid = forrige.Eid, // uendret — samme adresserbare seksjon, ny versjon
            Kildesystem = forrige.Kildesystem,
            KildeId = forrige.KildeId,
            ParentNodeId = forrige.ParentNodeId,
            NodeType = forrige.NodeType,
            Nummer = forrige.Nummer,
            Overskrift = overskrift,
            Tekst = sanertTekst,
            TekstHash = LovdataIdentifikatorer.BeregnTekstHash(sanertTekst),
            Sorteringsrekkefolge = forrige.Sorteringsrekkefolge,
            Versjon = forrige.Versjon + 1,
            Entitetsstatus = "gjeldende",
            ErstatterNodeId = forrige.Id,
        };
        var nyMetadata = new HandbokKommentarMetadataEntitet
        {
            NodeId = nyNode.Id,
            Dokumenttype = dokumenttype,
            Bindende = UtledBindende(dokumenttype),
            FesteNiva = festeNiva,
            Status = forrigeMetadata.Status, // statusløp (publisert/må revideres) endres via egne handlinger, ikke ved redigering
            Revisjonsgrunn = forrigeMetadata.Revisjonsgrunn,
            Publisert = forrigeMetadata.Publisert,
            SistFagligEndret = DateOnly.FromDateTime(DateTime.UtcNow),
            UnderoverskrifterJson = forrigeMetadata.UnderoverskrifterJson,
            Marginord = marginord?.ToList() ?? forrigeMetadata.Marginord,
            PraksisJson = forrigeMetadata.PraksisJson,
        };

        forrige.Entitetsstatus = "erstattet";
        db.RettskildeNoder.Add(nyNode);
        db.HandbokKommentarMetadata.Add(nyMetadata);

        // Lovreferansene er knyttet til den GAMLE noderaden (FraNodeId), som nå blir 'erstattet' — uten
        // dette ville en redigering stille og utilsiktet "mistet" koblingen til lovparagrafen fra
        // brukerens ståsted (den nye, gjeldende versjonen ville vist en tom referanseliste). Kopieres
        // fremover til den nye raden, samme "carry forward ved redigering"-mønster som metadata over.
        // De gamle radene røres ikke — den historiske (nå erstattede) versjonen beholder sine egne,
        // slik at "hvilke lovparagrafer kommenterte v1 faktisk på" fortsatt kan besvares presist.
        var forrigeReferanser = await db.RettskildeReferanser.Where(r => r.FraNodeId == forrige.Id).ToListAsync(ct);
        foreach (var r in forrigeReferanser)
        {
            db.RettskildeReferanser.Add(new RettskildeReferanseEntitet
            {
                Id = Guid.NewGuid(),
                FraNodeId = nyNode.Id,
                TilRettskildeId = r.TilRettskildeId,
                TilEid = r.TilEid,
            });
        }

        db.Proveniens.Add(ProveniensHjelper.NyRad("rettskilde_node", nyNode.Id, null, "endret", endretAv));
        await db.SaveChangesAsync(ct);
        return new HandbokNodeResultat(nyNode, nyMetadata);
    }

    /// <summary>Alle versjoner av en node (samme rettskilde+eid), nyeste først — AK-3.3.10 "Se tidligere versjoner".</summary>
    public Task<List<RettskildeNodeEntitet>> HentVersjonshistorikkAsync(Guid rettskildeId, string eid, CancellationToken ct = default) =>
        db.RettskildeNoder
            .Where(n => n.RettskildeId == rettskildeId && n.Eid == eid)
            .Include(n => n.HandbokMetadata)
            .OrderByDescending(n => n.Versjon)
            .ToListAsync(ct);

    /// <summary>Kobler en kommentar-seksjon til én paragraf i en Lov/Forskrift — samme mekanisme som interne Lovdata-kryssreferanser.</summary>
    public async Task<RettskildeReferanseEntitet> KobleLovreferanseAsync(
        Guid nodeId, Guid tilRettskildeId, string tilEid, CancellationToken ct = default)
    {
        if (!await db.RettskildeNoder.AnyAsync(n => n.Id == nodeId, ct))
        {
            throw new ArgumentException($"Node '{nodeId}' finnes ikke.");
        }
        if (!await db.RettskildeNoder.AnyAsync(n => n.RettskildeId == tilRettskildeId && n.Eid == tilEid, ct))
        {
            throw new ArgumentException($"Målnoden '{tilEid}' finnes ikke i rettskilde '{tilRettskildeId}'.");
        }
        if (await db.RettskildeReferanser.AnyAsync(r => r.FraNodeId == nodeId && r.TilRettskildeId == tilRettskildeId && r.TilEid == tilEid, ct))
        {
            throw new ArgumentException("Denne lovreferansen er allerede koblet.");
        }

        var referanse = new RettskildeReferanseEntitet { Id = Guid.NewGuid(), FraNodeId = nodeId, TilRettskildeId = tilRettskildeId, TilEid = tilEid };
        db.RettskildeReferanser.Add(referanse);
        await db.SaveChangesAsync(ct);
        return referanse;
    }

    public async Task<bool> FjernLovreferanseAsync(Guid referanseId, CancellationToken ct = default)
    {
        var referanse = await db.RettskildeReferanser.FirstOrDefaultAsync(r => r.Id == referanseId, ct);
        if (referanse is null) return false;
        db.RettskildeReferanser.Remove(referanse);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Manuell "Må revideres"-merking (AK-3.3.12, v1-forenkling — se docs/03-domenemodell.md §1.1.1).
    /// Automatisk flagging fra en fremtidig påvirkningsanalyse (byggesteg 8) er en senere utvidelse av
    /// samme felt, ingen skjemaendring.
    /// </summary>
    public async Task SettRevisjonsmerkeAsync(Guid nodeId, string revisjonsgrunn, string endretAv, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(revisjonsgrunn))
        {
            throw new ArgumentException("Revisjonsgrunn kan ikke være tom. Ingen gjettet fallback.");
        }
        var metadata = await HentMetadataForGjeldendeNodeAsync(nodeId, ct);
        metadata.Status = "ma_revideres";
        metadata.Revisjonsgrunn = revisjonsgrunn;
        db.Proveniens.Add(ProveniensHjelper.NyRad("rettskilde_node", nodeId, null, "endret", endretAv));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Publiserer en kommentar-seksjon. Bindende seksjoner krever registrert godkjenner (AK-3.3.11) —
    /// v1: fritekst i proveniens, samme mønster som `opprettet_av` ellers i systemet (ingen egen
    /// godkjenner-rolle/registry finnes ennå, jf. docs/03-domenemodell.md §2).
    /// </summary>
    public async Task PubliserKommentarAsync(Guid nodeId, string? godkjentAv, string endretAv, CancellationToken ct = default)
    {
        var metadata = await HentMetadataForGjeldendeNodeAsync(nodeId, ct);
        if (metadata.Bindende && string.IsNullOrWhiteSpace(godkjentAv))
        {
            throw new ArgumentException("Bindende seksjoner krever en registrert godkjenner før publisering.");
        }

        metadata.Status = "publisert";
        metadata.Publisert = DateOnly.FromDateTime(DateTime.UtcNow);
        var proveniensrad = ProveniensHjelper.NyRad("rettskilde_node", nodeId, null, "publisert", endretAv);
        proveniensrad.GodkjentAv = godkjentAv;
        db.Proveniens.Add(proveniensrad);
        await db.SaveChangesAsync(ct);
    }

    private async Task<HandbokKommentarMetadataEntitet> HentMetadataForGjeldendeNodeAsync(Guid nodeId, CancellationToken ct)
    {
        var node = await db.RettskildeNoder.FirstOrDefaultAsync(n => n.Id == nodeId && n.Entitetsstatus == "gjeldende", ct);
        if (node is null)
        {
            throw new ArgumentException($"Node '{nodeId}' finnes ikke eller er ikke gjeldende.");
        }
        var metadata = await db.HandbokKommentarMetadata.FirstOrDefaultAsync(m => m.NodeId == nodeId, ct);
        if (metadata is null)
        {
            throw new ArgumentException($"Node '{nodeId}' er ikke en håndbok-kommentarseksjon.");
        }
        return metadata;
    }

    private Task<RettskildeNodeEntitet?> HentGjeldendeNodeAsync(Guid nodeId, CancellationToken ct) =>
        db.RettskildeNoder.FirstOrDefaultAsync(n => n.Id == nodeId && n.Entitetsstatus == "gjeldende", ct);

    private async Task<int> NesteSorteringAsync(Guid rettskildeId, Guid? parentNodeId, CancellationToken ct) =>
        await db.RettskildeNoder.CountAsync(n => n.RettskildeId == rettskildeId && n.ParentNodeId == parentNodeId && n.Entitetsstatus == "gjeldende", ct);

    private static bool UtledBindende(string dokumenttype) => dokumenttype != "kommentar";

    private static void ValiderDokumenttype(string dokumenttype)
    {
        if (!GyldigeDokumenttyper.Contains(dokumenttype))
        {
            throw new ArgumentException($"Ukjent dokumenttype '{dokumenttype}'. Gyldige verdier: {string.Join(", ", GyldigeDokumenttyper)}.");
        }
    }

    private static void ValiderFesteNiva(string festeNiva)
    {
        if (!GyldigeFesteNivaer.Contains(festeNiva))
        {
            throw new ArgumentException($"Ukjent feste_niva '{festeNiva}'. Gyldige verdier: {string.Join(", ", GyldigeFesteNivaer)}.");
        }
    }

    /// <summary>
    /// Lokal eid-konvensjon for håndbøker: håndbøker har ingen ELI (`LovdataIdentifikatorer` er derfor
    /// ikke anvendelig), så eid bygges som en enkel foreldre-prefikset sti — samme "{forelder}/{segment}"-
    /// mønster som paragraf-/ledd-eId-ene i den ELI-forankrede konvensjonen (§1.2), bare uten ELI-roten.
    /// </summary>
    private static string LagEid(string? parentEid, string nummer) =>
        parentEid is null ? $"kap-{nummer}" : $"{parentEid}/{nummer}";

    private static string MinimalAknPlassholder(string tittel)
    {
        var tekst = System.Net.WebUtility.HtmlEncode(tittel);
        // v1-forenkling (se plan): statisk plassholder, ikke synkront regenerert per redigering —
        // rettskilde_noder er og blir autoritativ kilde for håndbokens navigasjon/lesing/tagging.
        // Tilfredsstiller kun CHECK-constrainten ck_rettskilder_akn_xml (non-null for importrolle='primaer').
        return $"""
            <akomaNtoso xmlns="http://docs.oasis-open.org/legaldocml/ns/akn/3.0">
              <doc name="rundskriv">
                <meta>
                  <proprietary source="#regel-ide">
                    <regelIde:kildetype>Rundskriv</regelIde:kildetype>
                    <regelIde:status>Utkast</regelIde:status>
                  </proprietary>
                </meta>
                <preface><p>{tekst}</p></preface>
                <body/>
              </doc>
            </akomaNtoso>
            """;
    }
}
