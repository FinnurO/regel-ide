using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #157] Oversikt over ALT som rammes av en kaskadesletting av én virksomhet — grunnlaget
/// for bekreftelsesdialogen frontend viser FØR selve slettingen utføres ("Denne virksomheten har X
/// tjenester, Y rettskilder, ... — alt slettes. Bekreft?"). Tallene dekker KUN det som faktisk slettes
/// (direkte eid av virksomheten via en FK som kaskaderer i DB-skjemaet, eller ryddes eksplisitt av
/// <see cref="VirksomhetSlettTjeneste.SlettAsync"/>) — IKKE nedstrøms detaljer som handlinger/vilkårstre
/// under hver tjeneste, som ville gjort tallene uleselige uten å endre selve avgjørelsen brukeren tar.
/// </summary>
public sealed record VirksomhetSlettOversikt(
    Guid VirksomhetId, string VirksomhetNavn,
    int Tjenester, int Rettskilder, int Begreper, int Navneformer, int Brukere,
    int Myndighetstildelinger, int VirksomhetKandidater, int VirksomhetRelasjoner,
    int VirksomhetNettsider, int Kodelister, int Datasett, int Vilkar, int Regelnoder, int Unntak,
    int VilkarstreKommentarer, int TekstTagger, int Hendelser,
    int KunnskapsbibliotekLenker, int KunnskapsbibliotekFiler, int UnderliggendeVirksomheter,
    /// <summary>
    /// Antall <see cref="TekstTaggEntitet"/> med <see cref="TekstTaggEntitet.RefId"/> satt ("publisert
    /// referanse", AK-3.3.4 — samme vern som <see cref="TekstTaggTjeneste.SlettAsync"/> allerede
    /// håndhever for enkelttagger). NÅR STØRRE ENN 0 BLOKKERER DETTE HELE SLETTINGEN — se
    /// <see cref="VirksomhetSlettTjeneste.SlettAsync"/>.
    /// </summary>
    int TekstTaggerMedPublisertReferanse)
{
    public bool KanSlettes => TekstTaggerMedPublisertReferanse == 0;
}

/// <summary>Resultat av selve slette-forsøket — se <see cref="VirksomhetSlettTjeneste.SlettAsync"/>.</summary>
public enum VirksomhetSlettUtfall
{
    Slettet,
    FinnesIkke,
    BlokkertAvPublisertReferanse,
    BlokkertAvUkjentReferanse,
}

public sealed record VirksomhetSlettResultat(VirksomhetSlettUtfall Utfall, string? Detalj = null);

/// <summary>
/// [Ny, issue #157] Kaskadesletting av en <see cref="Virksomhet"/> — ingen <c>DELETE</c>-vei fantes for
/// selve virksomhet-entiteten tidligere (kun for relasjoner/kandidatkø, se issue). De aller fleste av de
/// ~19 entitetstypene med en <c>VirksomhetId</c>-FK kaskaderer allerede i DB-skjemaet
/// (<c>OnDelete(Cascade)</c>, verifisert live mot <c>information_schema</c> under denne byggerunden —
/// IKKE antatt ut fra C#-koden alene, som noen steder mangler en eksplisitt <c>.OnDelete(...)</c> uten
/// at det faktisk betyr NO ACTION i skjemaet). Et fåtall FK-er er derimot <c>NO ACTION</c>/<c>RESTRICT</c>
/// og MÅ ryddes eksplisitt før selve virksomhet-raden slettes, ellers feiler hele operasjonen på et
/// FK-brudd: <see cref="Virksomhet.OverordnetEnhetId"/> (selvreferanse — barn mister foreldrekoblingen,
/// slettes IKKE selv), <see cref="VirksomhetRelasjonEntitet.TilVirksomhetId"/>,
/// <see cref="ProveniensEntitet.VirksomhetId"/>/<see cref="ProveniensEntitet.ForeslattAvVirksomhetId"/>
/// (nullstilles — proveniens er en logg, ikke virksomhetens eget arbeidsprodukt, se
/// <see cref="TjenesteregisterTjeneste.SlettForslagAsync"/> for et annet sted proveniens ryddes
/// eksplisitt), <see cref="KodelisteEntitet.VirksomhetId"/>, <see cref="DatasettVerdiEntitet.VirksomhetId"/>,
/// <see cref="HendelseEntitet.VirksomhetId"/>, og <see cref="BegrepEntitet.VirksomhetReferanseId"/>
/// (navneform-radene FOR denne virksomheten) og <see cref="RettskildeEntitet.VirksomhetId"/> (virksomhetens
/// EGNE lokale rettskilder — nasjonale/delte rettskilder har <c>VirksomhetId == null</c> og rammes aldri).
/// <para>
/// <b>Bevisst IKKE en fullstendig, statisk gjennomgang av ALLE tenkelige nedstrøms referanser</b> — flere
/// av disse "egne" tabellene (rettskilder, begreper, kodelister, hendelser) kan i prinsippet fortsatt
/// være referert fra EN HELT ANNEN virksomhets data (f.eks. en tjenesteavhengighet hos en annen
/// virksomhet som siterer en av denne virksomhetens hendelser, eller en annen virksomhets vilkår som
/// henter hjemmel fra en av denne virksomhetens rettskilder) — den slags kryssreferanser BLOKKERER
/// bevisst hele slettingen (se catch-blokken under) i stedet for å bli tvangsryddet, nøyaktig samme
/// avveining <see cref="TekstTaggTjeneste.SlettAsync"/> allerede gjør for publiserte referanser: et
/// FK-brudd her betyr "en annen virksomhets ekte, levende data avhenger av noe her" — ikke noe denne
/// operasjonen skal fjerne stille på en annen virksomhets vegne.
/// </para>
/// <para>
/// Ren <c>ExecuteDeleteAsync</c>/<c>ExecuteUpdateAsync</c> gjennomgående (aldri en sporet
/// <c>Remove</c> blandet inn) — samme, ALLEREDE etablerte mønster og samme begrunnelse som
/// <see cref="TjenesteregisterTjeneste.SlettForslagAsync"/> (se dens kommentar): en sporet slett-av-
/// hovedentitet etter et <c>ExecuteDeleteAsync</c> på en avhengighet kan utløse en falsk
/// <c>DbUpdateConcurrencyException</c> pga. fantomrader i endringssporingen.
/// </para>
/// </summary>
public sealed class VirksomhetSlettTjeneste(RegelIdeDbContext db)
{
    public async Task<VirksomhetSlettOversikt?> HentOversiktAsync(Guid virksomhetId, CancellationToken ct = default)
    {
        var virksomhet = await db.Virksomheter.FirstOrDefaultAsync(v => v.Id == virksomhetId, ct);
        if (virksomhet is null) return null;

        return new VirksomhetSlettOversikt(
            VirksomhetId: virksomhet.Id,
            VirksomhetNavn: virksomhet.Navn,
            Tjenester: await db.Tjenester.CountAsync(t => t.VirksomhetId == virksomhetId, ct),
            Rettskilder: await db.Rettskilder.CountAsync(r => r.VirksomhetId == virksomhetId, ct),
            Begreper: await db.Begreper.CountAsync(b => b.VirksomhetId == virksomhetId, ct),
            Navneformer: await db.Begreper.CountAsync(b => b.VirksomhetReferanseId == virksomhetId, ct),
            Brukere: await db.Brukere.CountAsync(b => b.VirksomhetId == virksomhetId, ct),
            Myndighetstildelinger: await db.Myndighetstildelinger.CountAsync(m => m.VirksomhetId == virksomhetId, ct),
            VirksomhetKandidater: await db.VirksomhetKandidater.CountAsync(k => k.VirksomhetId == virksomhetId, ct),
            VirksomhetRelasjoner: await db.VirksomhetRelasjoner
                .CountAsync(r => r.FraVirksomhetId == virksomhetId || r.TilVirksomhetId == virksomhetId, ct),
            VirksomhetNettsider: await db.VirksomhetNettsider.CountAsync(n => n.VirksomhetId == virksomhetId, ct),
            Kodelister: await db.Kodelister.CountAsync(k => k.VirksomhetId == virksomhetId, ct),
            Datasett: await db.Datasett.CountAsync(d => d.VirksomhetId == virksomhetId, ct),
            Vilkar: await db.Vilkar.CountAsync(v => v.VirksomhetId == virksomhetId, ct),
            Regelnoder: await db.Regelnoder.CountAsync(r => r.VirksomhetId == virksomhetId, ct),
            Unntak: await db.Unntak.CountAsync(u => u.VirksomhetId == virksomhetId, ct),
            VilkarstreKommentarer: await db.VilkarstreKommentarer.CountAsync(k => k.VirksomhetId == virksomhetId, ct),
            TekstTagger: await db.TekstTagger.CountAsync(t => t.VirksomhetId == virksomhetId, ct),
            Hendelser: await db.Hendelser.CountAsync(h => h.VirksomhetId == virksomhetId, ct),
            KunnskapsbibliotekLenker: await db.KunnskapsbibliotekLenker.CountAsync(k => k.VirksomhetId == virksomhetId, ct),
            KunnskapsbibliotekFiler: await db.KunnskapsbibliotekFiler.CountAsync(k => k.VirksomhetId == virksomhetId, ct),
            UnderliggendeVirksomheter: await db.Virksomheter.CountAsync(v => v.OverordnetEnhetId == virksomhetId, ct),
            TekstTaggerMedPublisertReferanse: await db.TekstTagger
                .CountAsync(t => t.VirksomhetId == virksomhetId && t.RefId != null, ct));
    }

    /// <summary>
    /// Utfører selve kaskadeslettingen — kalleren (endepunktet) MÅ ha fått et eksplisitt
    /// <c>?bekreft=true</c> fra brukeren FØRST (se docs/13-backlog.md §9-mønsteret "ingen stille
    /// destruksjon"). Blokkerer (uten å slette NOE) hvis noen av virksomhetens tekst-tagger har en
    /// publisert referanse, eller hvis en uforutsett FK fra en annen virksomhets data fortsatt peker inn.
    /// </summary>
    public async Task<VirksomhetSlettResultat> SlettAsync(Guid virksomhetId, CancellationToken ct = default)
    {
        var finnes = await db.Virksomheter.AnyAsync(v => v.Id == virksomhetId, ct);
        if (!finnes) return new VirksomhetSlettResultat(VirksomhetSlettUtfall.FinnesIkke);

        var publisertReferanser = await db.TekstTagger
            .CountAsync(t => t.VirksomhetId == virksomhetId && t.RefId != null, ct);
        if (publisertReferanser > 0)
        {
            return new VirksomhetSlettResultat(
                VirksomhetSlettUtfall.BlokkertAvPublisertReferanse,
                $"{publisertReferanser} tekst-tagg(er) har en publisert referanse (AK-3.3.4) og kan ikke fjernes. " +
                "Fjern/avpubliser disse referansene først, samme vern som gjelder enkelttagger.");
        }

        await using var transaksjon = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Rekkefølgen under er bevisst: der én av disse "egne" tabellene i prinsippet kan
            // refereres fra en ANNEN av virksomhetens egne rader (f.eks. en navneform som — usannsynlig,
            // men mulig — peker på en kodeliste), ryddes den mest avhengige raden FØRST. Kryssreferanser
            // fra en HELT ANNEN virksomhet fanges i stedet av catch-blokken under (se klassekommentaren).

            // Selvreferansen på virksomheter selv: barn mister foreldrekoblingen, slettes ikke.
            await db.Virksomheter.Where(v => v.OverordnetEnhetId == virksomhetId)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.OverordnetEnhetId, (Guid?)null), ct);

            // TilVirksomhetId er RESTRICT i DB — FraVirksomhetId er Cascade og trenger ikke ryddes her.
            await db.VirksomhetRelasjoner.Where(r => r.TilVirksomhetId == virksomhetId).ExecuteDeleteAsync(ct);

            // Proveniens er en logg, ikke virksomhetens eget arbeidsprodukt — nullstilles, slettes ikke.
            await db.Proveniens.Where(p => p.VirksomhetId == virksomhetId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.VirksomhetId, (Guid?)null), ct);
            await db.Proveniens.Where(p => p.ForeslattAvVirksomhetId == virksomhetId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.ForeslattAvVirksomhetId, (Guid?)null), ct);

            // Navneformer FØR kodelister/rettskilder de i prinsippet kunne referere.
            await db.Begreper.Where(b => b.VirksomhetReferanseId == virksomhetId).ExecuteDeleteAsync(ct);
            await db.Kodelister.Where(k => k.VirksomhetId == virksomhetId).ExecuteDeleteAsync(ct);
            await db.DatasettVerdier.Where(d => d.VirksomhetId == virksomhetId).ExecuteDeleteAsync(ct);
            await db.Hendelser.Where(h => h.VirksomhetId == virksomhetId).ExecuteDeleteAsync(ct);
            await db.Rettskilder.Where(r => r.VirksomhetId == virksomhetId).ExecuteDeleteAsync(ct);

            // Selve virksomheten — alt annet (tjenester, brukere, egne begreper, myndighetstildelinger,
            // virksomhetkandidater, virksomhet_nettsider, tekst_tagger, vilkår, regelnoder, unntak,
            // vilkarstre-kommentarer, kunnskapsbibliotek-lenker/filer, datasett, virksomhet_relasjoner
            // (fra), tjenesteavhengigheter (fra)) kaskaderer allerede i DB-skjemaet.
            var slettet = await db.Virksomheter.Where(v => v.Id == virksomhetId).ExecuteDeleteAsync(ct);
            if (slettet == 0)
            {
                await transaksjon.RollbackAsync(ct);
                return new VirksomhetSlettResultat(VirksomhetSlettUtfall.FinnesIkke);
            }

            await transaksjon.CommitAsync(ct);
            return new VirksomhetSlettResultat(VirksomhetSlettUtfall.Slettet);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23503" } pg)
        {
            await transaksjon.RollbackAsync(ct);
            return new VirksomhetSlettResultat(
                VirksomhetSlettUtfall.BlokkertAvUkjentReferanse,
                $"Blokkert av en referanse fra tabellen '{pg.TableName}' (constraint '{pg.ConstraintName}') — " +
                "trolig en ANNEN virksomhets data som fortsatt peker inn i denne. Ingenting ble slettet. " +
                "Må ryddes manuelt før denne virksomheten kan slettes.");
        }
        catch (PostgresException pg) when (pg.SqlState == "23503")
        {
            await transaksjon.RollbackAsync(ct);
            return new VirksomhetSlettResultat(
                VirksomhetSlettUtfall.BlokkertAvUkjentReferanse,
                $"Blokkert av en referanse fra tabellen '{pg.TableName}' (constraint '{pg.ConstraintName}') — " +
                "trolig en ANNEN virksomhets data som fortsatt peker inn i denne. Ingenting ble slettet. " +
                "Må ryddes manuelt før denne virksomheten kan slettes.");
        }
    }
}
