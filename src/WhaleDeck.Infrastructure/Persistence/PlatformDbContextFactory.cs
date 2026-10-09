using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WhaleDeck.Infrastructure.Persistence;

/// <summary>
/// Supplies a deterministic design-time context for generating migrations. The
/// connection is never opened by migration scaffolding and contains no secret.
/// </summary>
public sealed class PlatformDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=whaledeck_design;Username=whaledeck_design;Password=design-only")
            .Options;

        return new PlatformDbContext(options);
    }
}
