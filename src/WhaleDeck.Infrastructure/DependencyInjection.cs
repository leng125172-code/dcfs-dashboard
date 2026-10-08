using WhaleDeck.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WhaleDeck.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("WhaleDeck")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:WhaleDeck is required. Supply it through a secret-backed environment variable.");

        services.AddDbContextPool<PlatformDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3)));

        return services;
    }
}
