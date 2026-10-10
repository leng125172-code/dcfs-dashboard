using WhaleDeck.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Services;
using WhaleDeck.Infrastructure.Agent;
using WhaleDeck.Infrastructure.Catalog;
using WhaleDeck.Infrastructure.Identity;
using WhaleDeck.Infrastructure.Secrets;
using StackExchange.Redis;

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

        var valkeyEndpoint = configuration["Valkey:Endpoint"];
        if (string.IsNullOrWhiteSpace(valkeyEndpoint))
        {
            services.AddDistributedMemoryCache();
            services.AddSingleton<IOneTimeSecretStore, MemoryOneTimeSecretStore>();
        }
        else
        {
            var user = configuration["Valkey:User"];
            var password = configuration["Valkey:Password"];
            services.AddStackExchangeRedisCache(options =>
            {
                options.InstanceName = "whaledeck:";
                options.Configuration = $"{valkeyEndpoint},user={user},password={password},abortConnect=false";
            });
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(new ConfigurationOptions
            {
                EndPoints = { valkeyEndpoint },
                User = user,
                Password = password,
                AbortOnConnectFail = false
            }));
            services.AddSingleton<IOneTimeSecretStore, ValkeyOneTimeSecretStore>();
        }

        services.AddHttpClient("Authentik", client =>
        {
            client.BaseAddress = new Uri(configuration["Authentication:Authentik:ApiBaseUrl"] ?? "http://authentik:9000/api/v3/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddHttpClient("Xuanyuan", client =>
        {
            client.BaseAddress = new Uri(configuration["Catalog:Xuanyuan:BaseUrl"] ?? "https://xuanyuan.cloud/");
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WhaleDeck/0.1 (+internal-workstation)");
        });

        services.AddScoped<IPortalRepository, PortalRepository>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IGovernanceRepository, GovernanceRepository>();
        services.AddScoped<IMetricsQuery, MetricsQuery>();
        services.AddScoped<ResourceLeaseManager>();
        services.AddScoped<IIdentityDirectory, AuthentikIdentityDirectory>();
        services.AddScoped<IIdentityManager>(provider => provider.GetRequiredService<IIdentityDirectory>() as IIdentityManager
            ?? throw new InvalidOperationException("The identity directory does not support management operations."));
        services.AddScoped<ICatalogProvider, XuanyuanCatalogProvider>();
        services.AddSingleton<AgentGateway>();
        services.AddSingleton<IAgentGateway>(provider => provider.GetRequiredService<AgentGateway>());
        services.AddSingleton<IManagementQuery>(provider => provider.GetRequiredService<AgentGateway>());
        services.AddScoped<PortalService>();
        services.AddScoped<OverviewService>();
        services.AddScoped<OperationService>();
        services.AddScoped<GovernanceService>();

        return services;
    }
}
