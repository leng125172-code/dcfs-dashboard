using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;
using WhaleDeck.Domain.Entities;

namespace WhaleDeck.Infrastructure.Persistence;

public sealed class RoleMappingRepository(PlatformDbContext db) : IRoleMappingRepository
{
    public async Task<IReadOnlyCollection<RoleMapping>> ListAsync(CancellationToken cancellationToken) =>
        await db.RoleMappings.AsNoTracking()
            .OrderBy(item => item.AuthentikGroupNameSnapshot)
            .ThenBy(item => item.AuthentikGroupId)
            .ToArrayAsync(cancellationToken);

    public async Task<RoleMapping> SaveAsync(
        string actorSubject,
        string traceId,
        SaveRoleMappingCommand command,
        CancellationToken cancellationToken)
    {
        RoleMapping mapping;
        var created = command.Id is null;
        if (command.Id is { } id)
        {
            mapping = await db.RoleMappings.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException("Role mapping was not found.");
            if (mapping.Version != command.ExpectedVersion)
                throw new DbUpdateConcurrencyException("Role mapping was changed by another request.");
            mapping.AuthentikGroupId = command.AuthentikGroupId;
            mapping.AuthentikGroupNameSnapshot = command.AuthentikGroupName;
            mapping.IsEnabled = command.IsEnabled;
            mapping.Version++;
            mapping.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
        else
        {
            mapping = new RoleMapping
            {
                AuthentikGroupId = command.AuthentikGroupId,
                AuthentikGroupNameSnapshot = command.AuthentikGroupName,
                IsEnabled = command.IsEnabled
            };
            db.RoleMappings.Add(mapping);
        }
        db.AuditEvents.Add(new AuditEvent
        {
            ActorSubject = actorSubject,
            Action = created ? "identity.role-mapping.create" : "identity.role-mapping.update",
            TargetType = "RoleMapping",
            TargetId = command.AuthentikGroupId,
            Result = "Succeeded",
            TraceId = traceId,
            DetailJson = JsonSerializer.Serialize(new { command.AuthentikGroupName, command.IsEnabled })
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new InvalidOperationException("The Authentik group already has a role mapping.", exception);
        }
        return mapping;
    }

    public async Task DeleteAsync(
        string actorSubject,
        string traceId,
        Guid id,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var mapping = await db.RoleMappings.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Role mapping was not found.");
        if (mapping.Version != expectedVersion)
            throw new DbUpdateConcurrencyException("Role mapping was changed by another request.");
        db.RoleMappings.Remove(mapping);
        db.AuditEvents.Add(new AuditEvent
        {
            ActorSubject = actorSubject,
            Action = "identity.role-mapping.delete",
            TargetType = "RoleMapping",
            TargetId = mapping.AuthentikGroupId,
            Result = "Succeeded",
            TraceId = traceId,
            DetailJson = "{}"
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
