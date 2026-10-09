using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;

namespace WhaleDeck.Application.Services;

public sealed class PortalService(IPortalRepository repository)
{
    public async Task<IReadOnlyCollection<PortalItemDto>> ListAsync(string subject, bool administrator, CancellationToken cancellationToken) =>
        (await repository.ListVisibleAsync(subject, administrator, cancellationToken)).Select(Map).ToArray();

    public async Task<PortalItemDto> SaveAsync(string subject, bool administrator, SavePortalItemCommand command, CancellationToken cancellationToken)
    {
        Validate(command, administrator);
        return Map(await repository.SaveAsync(subject, administrator, command, cancellationToken));
    }

    public Task DeleteAsync(string subject, bool administrator, Guid id, CancellationToken cancellationToken) =>
        repository.DeleteAsync(subject, administrator, id, cancellationToken);

    private static void Validate(SavePortalItemCommand command, bool administrator)
    {
        if (command.Scope is not ("Personal" or "Public")) throw new ArgumentException("Portal scope must be Personal or Public.");
        if (command.Scope == "Public" && !administrator) throw new UnauthorizedAccessException("Only administrators can manage public portals.");
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Length > 80) throw new ArgumentException("Portal name is invalid.");
        if (!Uri.TryCreate(command.Url, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https")) throw new ArgumentException("Portal URL must use HTTP or HTTPS.");
        if (command.IconKind is not ("BuiltIn" or "RemoteImage")) throw new ArgumentException("Unsupported portal icon kind.");
    }

    private static PortalItemDto Map(Domain.Entities.PortalItem item) => new(
        item.Id, item.Scope, item.Name, item.Description, item.Url, item.IconKind,
        item.IconValue, item.Color, item.SortOrder, item.IsEnabled, item.Version);
}
