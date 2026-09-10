using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, tekst-tagg-departement-eierskap, 2026-08-31] Det ENESTE stedet i kodebasen som slår opp en
/// ekte <see cref="Virksomhet"/>-rad ved eksakt (case-insensitivt) navnematch mot en rå streng.
/// Innholdet er FLYTTET hit fra <c>RegelIde.Api.RettskildeRepository.FinnVirksomhetIdForNavnAsync</c>
/// (uendret logikk/dokumentasjon) — <c>RegelIde.Data</c> (som både <see cref="TekstTaggTjeneste"/> og
/// <see cref="NavnekandidatOppdagelseTjeneste"/> tilhører, og som nå trenger nøyaktig denne
/// oppslagslogikken for departement-eide tagger) kan IKKE referere <c>RegelIde.Api</c> —
/// prosjektreferansen går kun én vei (Api → Data, se RegelIde.Api.csproj). Flyttet i stedet for
/// duplisert: <c>RettskildeRepository.FinnVirksomhetIdForNavnAsync</c> delegerer nå hit.
/// </summary>
public sealed class VirksomhetOppslagTjeneste(RegelIdeDbContext db)
{
    /// <summary>
    /// Løser en rå navnestreng (f.eks. Lovdatas "ministry"-metadatafelt,
    /// <see cref="RettskildeEntitet.AnsvarligDepartement"/>) til en ekte <see cref="Virksomhet"/>-rad,
    /// ved eksakt (case-insensitivt) navnematch mot <see cref="Virksomhet.Navn"/> — IKKE via
    /// Begrep/navnekandidat-mekanismen (den er for tekst-OPPDAGELSE av navn i løpende lovtekst; her
    /// har kalleren allerede en strukturert, eksakt streng). Case-insensitivt fordi
    /// <see cref="OrganisasjonsregisterSeed"/> selv matcher case-insensitivt ved backfill (samme
    /// konvensjon, se dens klassekommentar: "eksisterende virksomhet med case-ufølsomt likt Navn").
    /// <c>.ToLower()</c> på begge sider (i stedet for <c>StringComparison.OrdinalIgnoreCase</c>, som EF
    /// Core ikke kan oversette til SQL) — oversettes til <c>LOWER(...)</c> og fungerer likt mot både
    /// Postgres og SQLite.
    /// <para>
    /// Returnerer null uten treff — «ingen gjettet fallback»: et navn som ikke finnes eksakt i
    /// katalogen (f.eks. en skrivemåte Brreg/regjeringen.no ikke bruker) forblir ukoblet, ALDRI
    /// koblet til nærmeste/mest sannsynlige treff.
    /// </para>
    /// </summary>
    public Task<Guid?> FinnVirksomhetIdForNavnAsync(string navn)
    {
        var navnLower = navn.ToLower();
        return db.Virksomheter.Where(v => v.Navn.ToLower() == navnLower).Select(v => (Guid?)v.Id).FirstOrDefaultAsync();
    }

    /// <summary>
    /// [Ny, fastsatt-av-runden, 2026-09-10, issue #215] Som <see cref="FinnVirksomhetIdForNavnAsync"/>,
    /// men prøver også virksomhetens NAVNEFORMER når registernavnet ikke traff.
    ///
    /// <para>
    /// Hvorfor det trengs: <see cref="Virksomhet.Navn"/> er Brregs egen form — store bokstaver, og av
    /// og til med en parentes: <c>NORGES VASSDRAGS- OG ENERGIDIREKTORAT (NVE)</c>. Lovdata skriver
    /// <c>Norges vassdrags- og energidirektorat</c>. Store bokstaver løses av det case-ufølsomme
    /// oppslaget, men parentesen gjør at navnet IKKE matcher — og det er nøyaktig hva navneformene
    /// finnes for. Målt 2026-09-10: oppslag mot navneformer i tillegg fanger bl.a.
    /// «Norges vassdrags- og energidirektorat» og «Direktoratet for samfunnssikkerhet og beredskap»,
    /// som registernavnet alene ikke finner.
    /// </para>
    ///
    /// <para>
    /// <b>Krever ETT treff.</b> Deler to virksomheter samme navneform, returneres <c>null</c> — et
    /// flertydig navn er ikke et svar (§3.3, «ingen gjettet fallback»). Målt er 1 av 690 unike
    /// navneformer flertydig, så kravet koster nesten ingenting og holder regelen ren.
    /// </para>
    ///
    /// <para>
    /// Holdt SEPARAT fra metoden over i stedet for å utvide den: <c>AnsvarligDepartement</c> har
    /// brukt registernavn-oppslaget siden 2026-08-30, og skal ikke plutselig løse flere navn som en
    /// bieffekt av denne runden.
    /// </para>
    /// </summary>
    public async Task<Guid?> FinnVirksomhetIdForNavnEllerNavneformAsync(string navn, CancellationToken ct = default)
    {
        var direkte = await FinnVirksomhetIdForNavnAsync(navn);
        if (direkte is not null) return direkte;

        var navnLower = navn.ToLower();
        var viaNavneform = await db.Begreper
            .Where(b => b.Begrepskategori == "virksomhet"
                        && b.Entitetsstatus == "gjeldende"
                        && b.VirksomhetReferanseId != null
                        && b.Term.ToLower() == navnLower)
            .Select(b => b.VirksomhetReferanseId!.Value)
            .Distinct()
            .Take(2)
            .ToListAsync(ct);

        return viaNavneform.Count == 1 ? viaNavneform[0] : null;
    }
}
