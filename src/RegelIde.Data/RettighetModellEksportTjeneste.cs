using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Eksporterer én Rettighet (Tjeneste) — inkl. alle dens Handlinger og Avhengigheter — som ett
/// JSON-objekt formet EKSAKT som <c>rettigheter[]</c>-elementene i den hånd-modellerte
/// <c>serveringsbevilling-modell-forslag.json</c> (samme feltnavn, snake_case, samme nøsting).
/// Formålet er å kunne verifisere, felt for felt, at det som faktisk ble bygget stemmer med det som
/// ble avtalt i modelleringsrunden — se docs/13-backlog.md og planen for Rettighet/Handling-runden.
/// <para>
/// Bevisst en HELT ANNEN eksport enn <see cref="TjenesteEksportTjeneste"/> (som er det eksisterende,
/// flate CPSV-lesedokumentet uten Handlinger/Innhold) — de tjener to forskjellige formål og skal
/// ikke slås sammen. Denne bygger rå <see cref="JsonObject"/>/<see cref="JsonArray"/>-trær direkte
/// (ikke POCO-er serialisert via API-ets vanlige camelCase-policy), siden modellfilens egen
/// konvensjon er snake_case — det er den konvensjonen som skal reproduseres her, ikke appens interne.
/// Ingen "_kommentar"/"_forklaring"-felt tas med — de er dokumentasjon av selve modelleringsprosessen,
/// ikke data. "id"/"eksisterende_tjeneste_id"-feltene fra modellfilen er heller ikke med her: denne
/// eksporten kommer FRA en ekte, allerede identifisert Tjeneste-rad, så id-en er alltid kjent (den er
/// input-parameteren), ikke noe å bekrefte i outputen.
/// </para>
/// </summary>
public sealed class RettighetModellEksportTjeneste(
    RegelIdeDbContext db, HandlingregisterTjeneste handlingregister, TjenesteavhengighetregisterTjeneste avhengighetregister)
{
    public async Task<JsonObject?> EksporterAsync(Guid tjenesteId, CancellationToken ct = default)
    {
        var tjeneste = await db.Tjenester.FirstOrDefaultAsync(t => t.Id == tjenesteId && t.Entitetsstatus == "gjeldende", ct);
        if (tjeneste is null) return null;

        var regelverksreferanser = await db.TjenesteRegelverksreferanser
            .Where(r => r.TjenesteId == tjenesteId).ToListAsync(ct);
        var rettskilder = await db.Rettskilder
            .Where(r => regelverksreferanser.Select(x => x.TilRettskildeId).Contains(r.Id)).ToListAsync(ct);
        var handlinger = await handlingregister.ListerForTjenesteAsync(tjenesteId, ct);
        var avhengigheter = await avhengighetregister.HentForTjenesteAsync(tjenesteId, ct);

        var innhold = tjeneste.InnholdJson is null
            ? null : JsonSerializer.Deserialize<TjenesteInnholdInput>(tjeneste.InnholdJson);

        var rot = new JsonObject
        {
            ["navn"] = tjeneste.Tittel,
            ["tjenesteomrade"] = tjeneste.Tjenesteomrade,
            ["los_klassifisering"] = tjeneste.LosKlassifisering,
            ["livshendelser"] = ArrStr(tjeneste.Livshendelser),
            ["type"] = tjeneste.Type,
            ["kompetent_myndighet"] = tjeneste.KompetentMyndighet,
            ["status"] = tjeneste.Status,
            ["malgruppe"] = ArrStr(tjeneste.Malgruppe),
            ["formal"] = tjeneste.Formal,
            ["innhold"] = InnholdJson(innhold, tjeneste.KonsekvensVedBrudd),
            ["regelverksreferanser"] = RegelverksreferanserJson(regelverksreferanser, rettskilder),
            ["handlinger"] = new JsonArray(handlinger.Select(HandlingJson).ToArray()),
            ["avhengigheter"] = new JsonArray(avhengigheter.Select(AvhengighetJson).ToArray()),
        };
        return rot;
    }

    private static JsonArray ArrStr(IEnumerable<string> verdier) => new(verdier.Select(v => (JsonNode)v).ToArray());

    private static JsonObject? HjemmelJson(HandlingHjemmelInput? h) =>
        h is null ? null : new JsonObject { ["lov"] = h.Lov, ["henvisning"] = h.Henvisning };

    private static JsonArray HjemmelListeJson(IReadOnlyList<HandlingHjemmelInput> liste) =>
        new(liste.Select(h => (JsonNode?)HjemmelJson(h)).ToArray());

    private static JsonObject? InnholdJson(TjenesteInnholdInput? i, string? konsekvensVedBrudd)
    {
        if (i is null) return null;
        var o = new JsonObject
        {
            ["tidspunkt_og_frister"] = i.TidspunktOgFrister,
            ["vedlegg"] = ArrStr(i.Vedlegg),
            ["vedlegg_merknad"] = i.VedleggMerknad,
            ["opplysninger_som_skal_sendes_inn"] = ArrStr(i.OpplysningerSomSkalSendesInn),
            ["opplysninger_merknad"] = i.OpplysningerMerknad,
            ["veiledning_og_utfylling"] = ArrStr(i.VeiledningOgUtfylling),
            ["veiledning_merknad"] = i.VeiledningMerknad,
        };
        if (i.InnsenderOgTilgang is { } ins)
        {
            o["innsender_og_tilgang"] = new JsonObject
            {
                ["hvem_kan_sende"] = ArrStr(ins.HvemKanSende),
                ["innlogging"] = ins.Innlogging,
            };
        }
        if (i.InnsendingOgOppfolging is { } send)
        {
            o["innsending_og_oppfolging"] = new JsonObject
            {
                ["kanal"] = send.Kanal,
                ["etter_mottak"] = ArrStr(send.EtterMottak),
                ["merknad"] = send.Merknad,
            };
        }
        if (i.KontaktOgHjelp is { } kontakt)
        {
            o["kontakt_og_hjelp"] = new JsonObject
            {
                ["generelt"] = kontakt.Generelt,
                ["kommunen_kan_veilede_om"] = ArrStr(kontakt.KommunenKanVeiledeOm),
            };
        }
        if (i.HvaRettighetenInnebarer is { } hvi)
        {
            var hviJson = new JsonObject
            {
                ["innledning"] = hvi.Innledning,
                ["varighet"] = hvi.Varighet,
                ["plikter"] = ArrStr(hvi.Plikter),
                ["kontroll_og_tilsyn"] = hvi.KontrollOgTilsyn,
                ["konsekvenser_ved_brudd_pa_regelverket"] = konsekvensVedBrudd,
                ["avgrensning_merknad"] = hvi.AvgrensningMerknad,
                ["krav_til_drift"] = hvi.KravTilDrift,
                ["tommeavtale_og_kontroll"] = hvi.TommeavtaleOgKontroll,
                ["rapportering"] = hvi.Rapportering,
            };
            if (hvi.EndringerIVirksomheten is { } end)
            {
                hviJson["endringer_i_virksomheten"] = new JsonObject
                {
                    ["plikt"] = end.Plikt,
                    ["eksempler"] = ArrStr(end.Eksempler),
                };
            }
            o["hva_rettigheten_innebarer"] = hviJson;
        }
        return o;
    }

    private static JsonArray RegelverksreferanserJson(
        IReadOnlyList<TjenesteRegelverksreferanseEntitet> referanser, IReadOnlyList<RettskildeEntitet> rettskilder)
    {
        var rettskildePerId = rettskilder.ToDictionary(r => r.Id);
        return new JsonArray(referanser.Select(r =>
        {
            var rettskilde = rettskildePerId.GetValueOrDefault(r.TilRettskildeId);
            return (JsonNode)new JsonObject
            {
                ["lov"] = rettskilde?.Kortnavn ?? rettskilde?.Tittel,
                ["henvisning"] = r.TilEid,
            };
        }).ToArray());
    }

    private static JsonNode HandlingJson(HandlingEntitet h)
    {
        var kanaler = JsonSerializer.Deserialize<List<HandlingKanalInput>>(h.KanalerJson) ?? [];
        var behandlingstid = JsonSerializer.Deserialize<HandlingBehandlingstidInput>(h.BehandlingstidJson);
        var kostnad = JsonSerializer.Deserialize<HandlingKostnadInput>(h.KostnadJson);
        var vedlegg = JsonSerializer.Deserialize<List<HandlingVedleggInput>>(h.VedleggJson) ?? [];
        var veiledningstekst = JsonSerializer.Deserialize<List<HandlingVeiledningstekstInput>>(h.VeiledningstekstJson) ?? [];
        var arsaker = JsonSerializer.Deserialize<List<HandlingArsakInput>>(h.ArsakerJson) ?? [];
        var resultat = JsonSerializer.Deserialize<HandlingResultatInput>(h.ResultatJson);

        var o = new JsonObject
        {
            ["navn"] = h.Navn,
            ["handlingstype"] = h.Handlingstype,
            ["bruksomraade"] = h.Bruksomraade,
            ["utfort_av"] = h.UtfortAv,
            ["merknad"] = h.Merknad,
        };
        if (kanaler.Count > 0)
        {
            o["kanaler"] = new JsonArray(kanaler.Select(k =>
                (JsonNode)new JsonObject { ["kanal"] = k.Kanal, ["adresse"] = k.Adresse }).ToArray());
        }
        if (behandlingstid is { Frist: not null } or { Hjemmel: not null })
        {
            o["behandlingstid"] = new JsonObject { ["frist"] = behandlingstid!.Frist, ["hjemmel"] = HjemmelJson(behandlingstid.Hjemmel) };
        }
        if (kostnad is { Belop: not null } || kostnad?.Hjemmel.Count > 0)
        {
            o["kostnad"] = new JsonObject { ["belop"] = kostnad!.Belop, ["hjemmel"] = HjemmelListeJson(kostnad.Hjemmel) };
        }
        if (vedlegg.Count > 0)
        {
            o["vedlegg"] = new JsonArray(vedlegg.Select(v =>
                (JsonNode)new JsonObject { ["navn"] = v.Navn, ["kategori"] = v.Kategori, ["hjemmel"] = HjemmelJson(v.Hjemmel) }).ToArray());
        }
        if (veiledningstekst.Count > 0)
        {
            o["veiledningstekst"] = new JsonArray(veiledningstekst.Select(v =>
                (JsonNode)new JsonObject { ["overskrift"] = v.Overskrift, ["innhold"] = v.Innhold, ["hjemmel"] = HjemmelJson(v.Hjemmel) }).ToArray());
        }
        if (arsaker.Count > 0)
        {
            o["arsaker"] = new JsonArray(arsaker.Select(a =>
                (JsonNode)new JsonObject { ["arsak"] = a.Arsak, ["hjemmel"] = HjemmelJson(a.Hjemmel) }).ToArray());
        }
        if (resultat is { Hva: not null } || resultat?.BevisKanaler.Count > 0)
        {
            o["resultat"] = new JsonObject
            {
                ["hva"] = resultat!.Hva,
                ["bevis_kanaler"] = new JsonArray(resultat.BevisKanaler.Select(b => (JsonNode)new JsonObject { ["kanal"] = b.Kanal }).ToArray()),
            };
        }
        return o;
    }

    private static JsonNode AvhengighetJson(TjenesteavhengighetVisning a)
    {
        var malType = a.MotpartTjenesteId is not null ? "tjeneste" : "ekstern_referanse";
        var o = new JsonObject
        {
            ["rel"] = a.Rel,
            ["retning"] = a.Retning,
            ["mal_type"] = malType,
            ["mal_navn"] = a.MotpartNavn,
        };
        if (a.MotpartTjenesteId is { } malId) o["mal_id"] = malId.ToString();
        if (a.MotpartOrganisasjonsnummer is { } orgnr) o["organisasjonsnummer"] = orgnr;
        if (a.MotpartUrl is { } url) o["kildeurl"] = url;
        if (a.Beskrivelse is { } beskrivelse) o["merknad"] = beskrivelse;
        return o;
    }
}
