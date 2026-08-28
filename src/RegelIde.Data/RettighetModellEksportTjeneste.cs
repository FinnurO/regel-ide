using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Eksporterer én eller flere Rettigheter (Tjenester) — inkl. alle deres Handlinger og Avhengigheter —
/// som JSON formet EKSAKT som <c>rettigheter[]</c>-elementene i den hånd-modellerte
/// <c>serveringsbevilling-modell-forslag.json</c> (samme feltnavn, snake_case, samme nøsting). Filen
/// selv ble aldri committet noe sted (ren modellutforskning, se docs/13-backlog.md §7) — navnet
/// lever videre kun som konvensjonen denne klassen reproduserer.
/// Formålet er tredelt (docs/23-tjeneste-modell-eksport-og-skjema.md): (1) intern verifisering, felt
/// for felt, at det som faktisk ble bygget stemmer med det som ble avtalt i modelleringsrunden,
/// (2) ekstern deling av en tjenestes fulle modell ut av applikasjonen, og (3) et fremtidig
/// importmål — det finnes IKKE en importmotpart ennå, se samme dokument.
/// <para>
/// Søsterklassen <see cref="TjenesteModellSkjema"/> bygger et JSON Schema for nøyaktig formen denne
/// klassen produserer (begge de to metodene under) — hold de to i synk ved feltendringer.
/// </para>
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
    RegelIdeDbContext db,
    HandlingTjenesteregisterTjeneste handlingTjenesteregister,
    TjenesteavhengighetregisterTjeneste avhengighetregister)
{
    public async Task<JsonObject?> EksporterAsync(Guid tjenesteId, CancellationToken ct = default)
    {
        var tjeneste = await db.Tjenester.FirstOrDefaultAsync(t => t.Id == tjenesteId && t.Entitetsstatus == "gjeldende", ct);
        return tjeneste is null ? null : await BygghetRettighetAsync(tjeneste, ct);
    }

    /// <summary>
    /// Eksporterer et SETT av tjenester i ett dokument: <c>{ "rettigheter": [...] }</c> — samme
    /// rotnøkkel som den (aldri committede) modellfilen selv brukte for flere rettigheter i én fil
    /// (bekreftet ved `rettigheter[0]`/`rettigheter[1]`-indeksering i <c>ServeringsbevillingModellSeed.cs</c>).
    /// IDer som ikke finnes (slettet, feil id, tilhører en annen virksomhet enn forventet) hoppes
    /// stille over — én ugyldig id skal ikke felle hele bulk-eksporten.
    /// </summary>
    public async Task<JsonObject> EksporterFlereAsync(IReadOnlyList<Guid> tjenesteIder, CancellationToken ct = default)
    {
        var tjenester = await db.Tjenester
            .Where(t => tjenesteIder.Contains(t.Id) && t.Entitetsstatus == "gjeldende")
            .ToListAsync(ct);
        var rettigheter = new JsonArray();
        foreach (var tjeneste in tjenester)
        {
            rettigheter.Add(await BygghetRettighetAsync(tjeneste, ct));
        }
        return new JsonObject { ["rettigheter"] = rettigheter };
    }

    private async Task<JsonObject> BygghetRettighetAsync(TjenesteEntitet tjeneste, CancellationToken ct)
    {
        var tjenesteId = tjeneste.Id;
        var regelverksreferanser = await db.TjenesteRegelverksreferanser
            .Where(r => r.TjenesteId == tjenesteId).ToListAsync(ct);
        var rettskilder = await db.Rettskilder
            .Where(r => regelverksreferanser.Select(x => x.TilRettskildeId).Contains(r.Id)).ToListAsync(ct);
        var handlinger = await handlingTjenesteregister.HentForTjenesteAsync(tjenesteId, ct);
        var avhengigheter = await avhengighetregister.HentForTjenesteAsync(tjenesteId, ct);

        var innhold = tjeneste.InnholdJson is null
            ? null : JsonSerializer.Deserialize<TjenesteInnholdInput>(tjeneste.InnholdJson);
        var egneInnholdselementer = JsonSerializer.Deserialize<List<EgetInnholdselementInput>>(tjeneste.EgneInnholdselementerJson) ?? [];

        return new JsonObject
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
            ["innhold"] = InnholdJson(innhold, tjeneste.KonsekvensVedBrudd, egneInnholdselementer),
            ["regelverksreferanser"] = RegelverksreferanserJson(regelverksreferanser, rettskilder),
            ["handlinger"] = new JsonArray(handlinger.Select(h => HandlingJson(h, tjenesteId)).ToArray()),
            ["avhengigheter"] = new JsonArray(avhengigheter.Select(AvhengighetJson).ToArray()),
        };
    }

    private static JsonArray ArrStr(IEnumerable<string> verdier) => new(verdier.Select(v => (JsonNode)v).ToArray());

    private static JsonObject? HjemmelJson(HandlingHjemmelInput? h) =>
        h is null ? null : new JsonObject { ["lov"] = h.Lov, ["henvisning"] = h.Henvisning };

    private static JsonArray HjemmelListeJson(IReadOnlyList<HandlingHjemmelInput> liste) =>
        new(liste.Select(h => (JsonNode?)HjemmelJson(h)).ToArray());

    private static JsonObject? InnholdJson(TjenesteInnholdInput? i, string? konsekvensVedBrudd, IReadOnlyList<EgetInnholdselementInput> egneInnholdselementer)
    {
        if (i is null && egneInnholdselementer.Count == 0) return null;
        var o = new JsonObject();
        if (i is not null)
        {
            o["tidspunkt_og_frister"] = i.TidspunktOgFrister;
            o["vedlegg"] = ArrStr(i.Vedlegg);
            o["vedlegg_merknad"] = i.VedleggMerknad;
            o["opplysninger_som_skal_sendes_inn"] = ArrStr(i.OpplysningerSomSkalSendesInn);
            o["opplysninger_merknad"] = i.OpplysningerMerknad;
            o["veiledning_og_utfylling"] = ArrStr(i.VeiledningOgUtfylling);
            o["veiledning_merknad"] = i.VeiledningMerknad;

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
        }
        if (egneInnholdselementer.Count > 0)
        {
            o["egne_innholdselementer"] = new JsonArray(egneInnholdselementer.Select(e =>
                (JsonNode)new JsonObject { ["id"] = e.Id, ["tittel"] = e.Tittel, ["tekst"] = e.Tekst }).ToArray());
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
                // Null = gjelder hele tjenesten (dagens flate liste); satt = knyttet til ett bestemt
                // felt — se feltnøkkel-konvensjonen i TjenesteregisterTjeneste.cs (TjenesteFeltnokler).
                ["felt"] = r.Felt,
            };
        }).ToArray());
    }

    private static JsonNode HandlingJson(HandlingEntitet h, Guid tjenesteId)
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
            // (2026-08-28) Handlinger kan nå være delt mellom flere tjenester (HandlingTjenesteEntitet,
            // "koblet", ikke eierskap) — se HandlingTjenesteregisterTjeneste sin klassekommentar. Denne
            // flagger om DENNE tjenesten (parameteren) faktisk eier handlingen (HandlingEntitet.TjenesteId)
            // eller bare har den koblet inn i sin Handlinger-fane.
            ["eies_av_denne_tjenesten"] = h.TjenesteId == tjenesteId,
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
