namespace RegelIde.Data.Tests;

/// <summary>
/// Delt embedded Postgres for hele assemblyen (samme fikset-port-problem som løst i
/// RegelIde.Api.Tests/ApiTestCollection — to IClassFixture-instanser med samme faste port kolliderer
/// når `dotnet test` kjører flere testklasser i samme prosjekt).
/// </summary>
[CollectionDefinition(Navn)]
public class DataTestCollection : ICollectionFixture<EmbeddedPostgresFixture>
{
    public const string Navn = "RegelIde.Data integrasjonstester";
}
