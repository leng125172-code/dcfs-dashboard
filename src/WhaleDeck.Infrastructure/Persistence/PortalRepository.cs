using Microsoft.EntityFrameworkCore;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;
using WhaleDeck.Domain.Entities;

namespace WhaleDeck.Infrastructure.Persistence;

public sealed class PortalRepository(PlatformDbContext dbContext) : IPortalRepository
{
    public async Task<IReadOnlyCollection<PortalItem>> ListVisibleAsync(string subject, bool administrator, CancellationToken cancellationToken)
    {
        var hidden = dbContext.PublicPortalPreferences
            .Where(item => item.Subject == subject && item.IsHidden)
            .Select(item => item.PortalItemId);
        return await dbContext.PortalItems.AsNoTracking()
            .Where(item => item.IsEnabled &&
                ((item.Scope == "Personal" && item.OwnerSubject == subject) ||
                 (item.Scope == "Public" && !hidden.Contains(item.Id))))
            .OrderBy(item => item.Scope).ThenBy(item => item.SortOrder).ThenBy(item => item.Name)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<PortalItem> SaveAsync(string subject, bool administrator, SavePortalItemCommand command, CancellationToken cancellationToken)
    {
        PortalItem entity;
        if (command.Id is { } id)
        {
            entity = await dbContext.PortalItems.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException("Portal item was not found.");
            if (entity.Scope == "Public" && !administrator || entity.Scope == "Personal" && entity.OwnerSubject != subject)
            {
                throw new UnauthorizedAccessException("Portal item is outside the caller scope.");
            }
            if (command.ExpectedVersion != entity.Version)
            {
                throw new InvalidOperationException("Portal item was modified by another request.");
            }
            entity.Scope = command.Scope;
            entity.OwnerSubject = command.Scope == "Personal" ? subject : null;
            entity.Name = command.Name.Trim();
            entity.Description = command.Description?.Trim();
            entity.Url = command.Url;
            entity.IconKind = command.IconKind;
            entity.IconValue = command.IconValue;
            entity.Color = command.Color;
            entity.SortOrder = command.SortOrder;
            entity.IsEnabled = command.IsEnabled;
            entity.Version++;
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
        else
        {
            entity = new PortalItem
            {
                Scope = command.Scope,
                OwnerSubject = command.Scope == "Personal" ? subject : null,
                Name = command.Name.Trim(),
                Description = command.Description?.Trim(),
                Url = command.Url,
                IconKind = command.IconKind,
                IconValue = command.IconValue,
                Color = command.Color,
                SortOrder = command.SortOrder,
                IsEnabled = command.IsEnabled
            };
            dbContext.PortalItems.Add(entity);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(string subject, bool administrator, Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.PortalItems.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Portal item was not found.");
        if (entity.Scope == "Public" && !administrator || entity.Scope == "Personal" && entity.OwnerSubject != subject)
        {
            throw new UnauthorizedAccessException("Portal item is outside the caller scope.");
        }
        dbContext.PortalItems.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
