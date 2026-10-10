using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;

namespace WhaleDeck.Application.Services;

public sealed class RoleMappingService(
    IRoleMappingRepository repository,
    IIdentityDirectory identity,
    IAdministratorGroupConfiguration administratorGroup)
{
    public async Task<IReadOnlyCollection<RoleMappingDto>> ListAsync(CancellationToken cancellationToken)
    {
        var configuredId = administratorGroup.GroupId;
        var mappings = await repository.ListAsync(cancellationToken);
        var result = mappings
            .Where(item => !string.Equals(item.AuthentikGroupId, configuredId, StringComparison.Ordinal))
            .Select(Map)
            .ToList();
        if (!string.IsNullOrWhiteSpace(configuredId))
        {
            result.Insert(0, new RoleMappingDto(
                null,
                configuredId,
                configuredId,
                "Administrator",
                true,
                0,
                "Deployment",
                false,
                null));
        }
        return result;
    }

    public async Task<RoleMappingDto> SaveAsync(
        string actorSubject,
        string traceId,
        SaveRoleMappingCommand command,
        CancellationToken cancellationToken)
    {
        Validate(command);
        if (string.Equals(command.AuthentikGroupId, administratorGroup.GroupId, StringComparison.Ordinal))
            throw new InvalidOperationException("The deployment administrator group is immutable.");

        var groups = await identity.ListGroupsAsync(cancellationToken);
        var group = groups.SingleOrDefault(item => string.Equals(item.Id, command.AuthentikGroupId, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException("The Authentik group was not found.");
        var normalized = command with { AuthentikGroupName = group.Name };
        return Map(await repository.SaveAsync(actorSubject, traceId, normalized, cancellationToken));
    }

    public async Task DeleteAsync(
        string actorSubject,
        string traceId,
        Guid id,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var mapping = (await repository.ListAsync(cancellationToken)).SingleOrDefault(item => item.Id == id)
            ?? throw new KeyNotFoundException("Role mapping was not found.");
        if (string.Equals(mapping.AuthentikGroupId, administratorGroup.GroupId, StringComparison.Ordinal))
            throw new InvalidOperationException("The deployment administrator group is immutable.");
        await repository.DeleteAsync(actorSubject, traceId, id, expectedVersion, cancellationToken);
    }

    private static void Validate(SaveRoleMappingCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.AuthentikGroupId) || command.AuthentikGroupId.Length > 128 || command.AuthentikGroupId.Any(char.IsControl))
            throw new ArgumentException("Authentik group id is invalid.");
        if (string.IsNullOrWhiteSpace(command.AuthentikGroupName) || command.AuthentikGroupName.Length > 150 || command.AuthentikGroupName.Any(char.IsControl))
            throw new ArgumentException("Authentik group name is invalid.");
        if (command.Id is not null && command.ExpectedVersion is null)
            throw new ArgumentException("Expected version is required when updating a role mapping.");
    }

    private static RoleMappingDto Map(Domain.Entities.RoleMapping item) => new(
        item.Id,
        item.AuthentikGroupId,
        item.AuthentikGroupNameSnapshot,
        item.Role,
        item.IsEnabled,
        item.Version,
        "Database",
        true,
        item.UpdatedAtUtc);
}
