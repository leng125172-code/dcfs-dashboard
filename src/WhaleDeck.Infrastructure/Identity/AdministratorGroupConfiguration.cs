using Microsoft.Extensions.Configuration;
using WhaleDeck.Application.Abstractions;

namespace WhaleDeck.Infrastructure.Identity;

public sealed class AdministratorGroupConfiguration(IConfiguration configuration) : IAdministratorGroupConfiguration
{
    public string GroupId { get; } = configuration["Authentication:Authentik:AdministratorGroupId"]
        ?? configuration["Authentication:Authentik:AdministratorGroupName"]
        ?? string.Empty;
}
