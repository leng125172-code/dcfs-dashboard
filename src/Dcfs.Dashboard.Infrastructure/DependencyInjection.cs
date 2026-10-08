using Dcfs.Dashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dcfs.Dashboard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Dashboard")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Dashboard is required. Supply it through a secret-backed environment variable.");

        services.AddDbContextPool<DashboardDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3)));

        return services;
    }
}
