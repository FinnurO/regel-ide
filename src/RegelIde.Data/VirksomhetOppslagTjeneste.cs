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
}
