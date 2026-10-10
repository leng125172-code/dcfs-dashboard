namespace WhaleDeck.Application.Models;

public sealed record RoleMappingDto(
    Guid? Id,
    string AuthentikGroupId,
    string AuthentikGroupName,
    string Role,
    bool IsEnabled,
    long Version,
    string Source,
    bool IsMutable,
    DateTimeOffset? UpdatedAtUtc);

public sealed record SaveRoleMappingCommand(
    Guid? Id,
    string AuthentikGroupId,
    string AuthentikGroupName,
    bool IsEnabled,
    long? ExpectedVersion);
