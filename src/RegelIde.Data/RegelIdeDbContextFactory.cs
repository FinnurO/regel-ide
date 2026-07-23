using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RegelIde.Data;

/// <summary>Kun for design-time (migrations-generering via `dotnet ef`) — selve appen setter opp DbContext via DI (se RegelIde.Api).</summary>
public sealed class RegelIdeDbContextFactory : IDesignTimeDbContextFactory<RegelIdeDbContext>
{
    public RegelIdeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RegelIdeDbContext>()
            .UseNpgsql("Host=localhost;Database=regelide_design_time;Username=postgres;Password=postgres")
            .Options;
        return new RegelIdeDbContext(options);
    }
}
