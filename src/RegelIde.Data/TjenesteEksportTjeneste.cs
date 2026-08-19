namespace RegelIde.Data;

/// <summary>
/// (2026-08-20) Komponerer ÉN samlet eksport av en tjeneste og alt den er koblet til — se
/// <c>TjenesteEksportDto</c> (RegelIde.Api) for hvorfor og hva formålet er. Ren LESE-komposisjon:
/// slår sammen resultatene fra registertjenestene som allerede finnes, ingen egen lagret tilstand,
/// ingen ny skjemaform. Returnerer entiteter (ikke DTO-er) — API-laget mapper til DTO-er, samme
/// lagdeling som resten av kodebasen.
/// <para>
/// **BEVISST uten vilkårstre** (2026-08-20, rettet etter Johanns tilbakemelding): et tidligere
/// utkast av denne klassen bygde også med <see cref="TjenesteEntitet.RotnodeId"/>s regelnode-tre.
/// Det var feil scope for det denne eksporten faktisk skal bevise — kjernemodellen (Tjeneste,
/// tjenesteavhengigheter, eksterne referanser), IKKE vilkårstreet, som er et bevisst separat,
/// senere arbeid (samme "avgrenset, ikke big-bang"-prinsipp som resten av prosjektet — vilkårstreet
/// kobles til en tjeneste via ETT nullbart FK-felt, det er ikke tett vevd sammen med
/// kjernemodellen bare fordi samme skjermbilde i dag viser begge). Å ta det med her ville antydet at
/// vilkårstreet er en del av det som avklares nå, når det ikke er det.
/// </para>
/// </summary>
public sealed class TjenesteEksportTjeneste(
    TjenesteregisterTjeneste tjenesteregister,
    HendelseregisterTjeneste hendelseregister,
    TjenesteavhengighetregisterTjeneste avhengighetregister,
    RegelIdeDbContext db)
{
    public sealed record Eksport(
        TjenesteEntitet Tjeneste, string VirksomhetNavn,
        IReadOnlyList<TjenesteRegelverksreferanseEntitet> Regelverksreferanser, IReadOnlyList<HendelseEntitet> Hendelser,
        IReadOnlyList<TjenesteavhengighetVisning> Avhengigheter, DateTimeOffset EksportertTidspunkt);

    public async Task<Eksport?> EksporterAsync(Guid tjenesteId, CancellationToken ct = default)
    {
        var tjeneste = await tjenesteregister.FinnAsync(tjenesteId, ct);
        if (tjeneste is null) return null;

        var virksomhet = await db.Virksomheter.FindAsync([tjeneste.VirksomhetId], ct);
        var regelverksreferanser = await tjenesteregister.RegelverksreferanserForAsync(tjenesteId, ct);
        var hendelser = await hendelseregister.ListerForTjenesteAsync(tjenesteId, ct);
        var avhengigheter = await avhengighetregister.HentForTjenesteAsync(tjenesteId, ct);

        return new Eksport(
            tjeneste, virksomhet?.Navn ?? "(ukjent virksomhet)", regelverksreferanser, hendelser,
            avhengigheter, DateTimeOffset.UtcNow);
    }
}
