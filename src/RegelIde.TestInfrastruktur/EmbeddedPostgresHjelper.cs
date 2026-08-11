using Npgsql;

namespace RegelIde.TestInfrastruktur;

/// <summary>
/// Delt mellom <c>RegelIde.Data.Tests</c> sin <c>EmbeddedPostgresFixture</c> og
/// <c>RegelIde.Api.Tests</c> sin <c>EmbeddedPostgresApiFixture</c> (GitHub-issue #10, "Flaky: hele
/// RegelIde.Data.Tests faller ved fullkjøring av løsningen").
/// <para>
/// <c>PgServer.Start()</c> returnerer før Postgres faktisk tar imot tilkoblinger (serveren rapporterer
/// "57P03 the database system is starting up" en liten stund etter oppstart). Under normal last rekker
/// serveren å bli klar før første kall — men når `dotnet test` kjører flere testprosjekter i parallell
/// og flere embedded Postgres-instanser starter samtidig, blir oppstarten treg nok til at den aller
/// første tilkoblingen (typisk et <c>CREATE DATABASE</c>-kall rett etter <c>Start()</c>) timer ut.
/// Ikke-deterministisk — derav "flaky", ikke en ekte feil i produksjonskoden.
/// </para>
/// <para>
/// Fantes tidligere som to nesten-identiske private metoder i de to fixturene, med risiko for å drifte
/// fra hverandre (og de gjorde det — kun <c>EmbeddedPostgresApiFixture</c> hadde ventingen). Ett delt
/// sted i stedet.
/// </para>
/// </summary>
public static class EmbeddedPostgresHjelper
{
    public static async Task VentTilKlarAsync(string connString, int maksAntallForsok = 20, int forsinkelseMs = 250)
    {
        for (var forsok = 1; forsok <= maksAntallForsok; forsok++)
        {
            try
            {
                await using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();
                return;
            }
            catch (PostgresException) when (forsok < maksAntallForsok)
            {
                await Task.Delay(forsinkelseMs);
            }
            catch (NpgsqlException) when (forsok < maksAntallForsok)
            {
                await Task.Delay(forsinkelseMs);
            }
        }
    }
}
