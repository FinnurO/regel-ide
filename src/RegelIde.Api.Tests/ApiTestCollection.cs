namespace RegelIde.Api.Tests;

/// <summary>
/// Delt embedded Postgres-instans for hele RegelIde.Api.Tests-assemblyen — starter/stopper den
/// tunge Postgres-prosessen kun én gang i stedet for én gang per testklasse (sekvensiell
/// restart per klasse viste seg upålitelig i dette miljøet, se git-historikk).
/// </summary>
[CollectionDefinition(Navn)]
public class ApiTestCollection : ICollectionFixture<EmbeddedPostgresApiFixture>
{
    public const string Navn = "RegelIde.Api integrasjonstester";
}
