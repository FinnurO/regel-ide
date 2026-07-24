using Xunit;

// Hver testklasse her starter sin egen embedded Postgres-instans på en fast port (se
// EmbeddedPostgresApiFixture) — xUnits standard parallellisering på tvers av klasser i samme
// assembly ville latt to slike instanser kollidere på samme port samtidig. Sekvensiell kjøring er
// et greit bytte gitt at vi uansett bare har noen få testklasser her.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
