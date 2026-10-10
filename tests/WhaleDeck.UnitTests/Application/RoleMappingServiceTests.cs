using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;
using WhaleDeck.Application.Services;
using WhaleDeck.Domain.Entities;

namespace WhaleDeck.UnitTests.Application;

public sealed class RoleMappingServiceTests
{
    [Fact]
    public async Task DeploymentMappingIsListedAsImmutable()
    {
        var service = new RoleMappingService(
            new RoleMappingRepositoryStub([]),
            new IdentityDirectoryStub([]),
            new AdministratorGroupConfigurationStub("bootstrap-group"));

        var mapping = Assert.Single(await service.ListAsync(default));

        Assert.Equal("bootstrap-group", mapping.AuthentikGroupId);
        Assert.Equal("Deployment", mapping.Source);
        Assert.False(mapping.IsMutable);
    }

    [Fact]
    public async Task SaveUsesAuthoritativeAuthentikGroupName()
    {
        var repository = new RoleMappingRepositoryStub([]);
        var service = new RoleMappingService(
            repository,
            new IdentityDirectoryStub([
                new ManagedResourceDto("group-id", "Platform Operators", "AuthentikGroup", "Active", string.Empty, true, new Dictionary<string, string>())
            ]),
            new AdministratorGroupConfigurationStub("bootstrap-group"));

        var saved = await service.SaveAsync("admin", "trace", new SaveRoleMappingCommand(null, "group-id", "untrusted-name", true, null), default);

        Assert.Equal("Platform Operators", saved.AuthentikGroupName);
        Assert.Equal("Platform Operators", repository.Saved?.AuthentikGroupName);
    }

    [Fact]
    public async Task DeploymentMappingCannotBeRecreatedAsMutable()
    {
        var service = new RoleMappingService(
            new RoleMappingRepositoryStub([]),
            new IdentityDirectoryStub([]),
            new AdministratorGroupConfigurationStub("bootstrap-group"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(
            "admin", "trace", new SaveRoleMappingCommand(null, "bootstrap-group", "Administrators", true, null), default));
    }

    private sealed class RoleMappingRepositoryStub(IReadOnlyCollection<RoleMapping> mappings) : IRoleMappingRepository
    {
        public SaveRoleMappingCommand? Saved { get; private set; }
        public Task<IReadOnlyCollection<RoleMapping>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(mappings);
        public Task<RoleMapping> SaveAsync(string actorSubject, string traceId, SaveRoleMappingCommand command, CancellationToken cancellationToken)
        {
            Saved = command;
            return Task.FromResult(new RoleMapping
            {
                AuthentikGroupId = command.AuthentikGroupId,
                AuthentikGroupNameSnapshot = command.AuthentikGroupName,
                IsEnabled = command.IsEnabled
            });
        }
        public Task DeleteAsync(string actorSubject, string traceId, Guid id, long expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class AdministratorGroupConfigurationStub(string groupId) : IAdministratorGroupConfiguration
    {
        public string GroupId { get; } = groupId;
    }

    private sealed class IdentityDirectoryStub(IReadOnlyCollection<ManagedResourceDto> groups) : IIdentityDirectory
    {
        public Task<IReadOnlyCollection<ManagedResourceDto>> ListGroupsAsync(CancellationToken cancellationToken) => Task.FromResult(groups);
        public Task<CurrentUserDto> ResolveCurrentAsync(string subject, string? name, IReadOnlyCollection<string> claimGroups, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<ManagedResourceDto>> ListUsersAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<ManagedResourceDto>> ListSsoApplicationsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
