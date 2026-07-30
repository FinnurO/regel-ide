using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api;

/// <summary>
/// Bygger den tjenestesentrerte veiledningsvisningen (docs/12-fasit-handbok-leveranse.md "Hovedfunn",
/// 2026-07-30) — en lineær, beslutnings-ordnet fortelling om et vilkårstre, med kommunale/nasjonale
/// datasett-verdier og veiledningskommentarer vevd inn per node. Samme "hent alt flatt, bygg treet i
/// minnet"-mønster som klienten allerede bruker i <c>bygging.ts</c> (parent→barn→unntak i samme
/// rekkefølge) — nå på serveren, og komplett med data som klienten uansett måtte slått opp per node.
/// Returnerer DTO-treet direkte (i stedet for entiteter mappet i Program.cs, som resten av API-et
/// gjør) — selve poenget med denne repositoryen ER den rekursive DTO-formen.
/// </summary>
public sealed class VeiledningRepository(RegelIdeDbContext db)
{
    public async Task<VeiledningDto?> ByggAsync(Guid tjenesteId, Guid? virksomhetId, CancellationToken ct = default)
    {
        var tjeneste = await db.Tjenester.FirstOrDefaultAsync(t => t.Id == tjenesteId && t.Entitetsstatus == "gjeldende", ct);
        if (tjeneste?.RotnodeId is not { } rotnodeId) return null;

        var regelnoder = await db.Regelnoder.Where(r => r.Entitetsstatus == "gjeldende").ToDictionaryAsync(r => r.Id, ct);
        var vilkarene = await db.Vilkar.Where(v => v.Entitetsstatus == "gjeldende").ToDictionaryAsync(v => v.Id, ct);
        var barnPerRegelnode = (await db.RegelnodeBarn.OrderBy(b => b.Rekkefolge).ToListAsync(ct))
            .GroupBy(b => b.RegelnodeId).ToDictionary(g => g.Key, g => g.ToList());
        var unntakPerGjelderRegel = (await db.Unntak.Where(u => u.Entitetsstatus == "gjeldende").ToListAsync(ct))
            .GroupBy(u => u.GjelderRegelId).ToDictionary(g => g.Key, g => g.ToList());
        var kommentarPerMal = (await db.VilkarstreKommentarer.OrderBy(k => k.Rekkefolge).ToListAsync(ct))
            .GroupBy(k => (k.MalType, k.MalId)).ToDictionary(g => g.Key, g => g.Select(VilkarstreKommentarDto.FraEntitet).ToList());
        var datasettPerVilkar = (await db.VilkarInputDatasett.ToListAsync(ct))
            .GroupBy(i => i.VilkarId).ToDictionary(g => g.Key, g => g.Select(i => i.DatasettId).ToList());
        var datasett = await db.Datasett.ToDictionaryAsync(d => d.Id, ct);
        var verdiPerDatasett = (await db.DatasettVerdier.ToListAsync(ct))
            .GroupBy(v => v.DatasettId).ToDictionary(g => g.Key, g => g.ToList());

        List<VilkarstreKommentarDto> HentKommentarer(string malType, Guid malId) =>
            kommentarPerMal.GetValueOrDefault((malType, malId), []);

        List<VeiledningDatasettVerdiDto> HentInputVerdier(Guid vilkarId) =>
            datasettPerVilkar.GetValueOrDefault(vilkarId, [])
                .Select(datasettId =>
                {
                    var felt = datasett[datasettId];
                    var rader = verdiPerDatasett.GetValueOrDefault(datasettId, []);
                    // §8.4-mønsteret: kommune-spesifikk verdi hvis den finnes, ellers den nasjonale
                    // standardverdien (VirksomhetId=null) — fallback-logikken ligger her, ETT sted, i
                    // stedet for i hvert frontend-kall.
                    var kommuneVerdi = virksomhetId is not null ? rader.FirstOrDefault(v => v.VirksomhetId == virksomhetId) : null;
                    var standardVerdi = rader.FirstOrDefault(v => v.VirksomhetId is null);
                    var valgt = kommuneVerdi ?? standardVerdi;
                    return valgt is null
                        ? null
                        : new VeiledningDatasettVerdiDto(datasettId, felt.Felt, felt.Prop, valgt.VerdiJson, valgt.Kilde, kommuneVerdi is null);
                })
                .Where(v => v is not null).Select(v => v!).ToList();

        string TittelFor(string type, Guid id) => type == "vilkar" ? vilkarene[id].Tittel : regelnoder[id].Tittel;

        VeiledningNodeDto BesokVilkar(VilkarEntitet v) => new(
            v.Id, "vilkar", v.Tittel, v.Beskrivelse, v.Vilkarstype, v.Vurderingstype,
            JsonSerializer.Deserialize<List<SkjonnsmomentInput>>(v.SkjonnsmomenterJson) ?? [], null,
            JsonSerializer.Deserialize<List<JuridiskGrunnlagInput>>(v.JuridiskGrunnlagJson) ?? [],
            HentInputVerdier(v.Id), HentKommentarer("vilkar", v.Id), [], []);

        VeiledningNodeDto BesokRegelnode(RegelnodeEntitet r)
        {
            var barnDto = barnPerRegelnode.GetValueOrDefault(r.Id, [])
                .Select(b => b.BarnType == "vilkar" ? BesokVilkar(vilkarene[b.BarnId]) : BesokRegelnode(regelnoder[b.BarnId]))
                .ToList();
            var unntakDto = unntakPerGjelderRegel.GetValueOrDefault(r.Id, [])
                .Select(u => new VeiledningUnntakDto(
                    u.Id, u.Tittel, u.Beskrivelse, u.BetingelseType, u.BetingelseId, TittelFor(u.BetingelseType, u.BetingelseId),
                    HentKommentarer("unntak", u.Id)))
                .ToList();
            return new VeiledningNodeDto(
                r.Id, "regelnode", r.Tittel, r.Beskrivelse, null, null, [], r.BarnOperator,
                JsonSerializer.Deserialize<List<JuridiskGrunnlagInput>>(r.JuridiskGrunnlagJson) ?? [],
                [], HentKommentarer("regelnode", r.Id), barnDto, unntakDto);
        }

        var rot = BesokRegelnode(regelnoder[rotnodeId]);
        return new VeiledningDto(tjeneste.Id, tjeneste.Tittel, virksomhetId, rot);
    }
}
