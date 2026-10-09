using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;
using WhaleDeck.Application.Services;
using WhaleDeck.Domain.Entities;

namespace WhaleDeck.UnitTests.Application;

public sealed class PortalServiceTests
{
    [Fact]
    public async Task NonAdministratorCannotPublishPublicPortal()
    {
        var service = new PortalService(new NoOpPortalRepository());
        var command = new SavePortalItemCommand(null, "Public", "Docs", null, "http://192.168.22.19/", "BuiltIn", "docs", "primary", 0, true, null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveAsync("subject", false, command, default));
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not-a-url")]
    public async Task PortalRejectsUnsafeUrlSchemes(string url)
    {
        var service = new PortalService(new NoOpPortalRepository());
        var command = new SavePortalItemCommand(null, "Personal", "Docs", null, url, "BuiltIn", "docs", "primary", 0, true, null);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync("subject", false, command, default));
    }

    private sealed class NoOpPortalRepository : IPortalRepository
    {
        public Task<IReadOnlyCollection<PortalItem>> ListVisibleAsync(string subject, bool administrator, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<PortalItem>>([]);

        public Task<PortalItem> SaveAsync(string subject, bool administrator, SavePortalItemCommand command, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Validation should run before persistence.");

        public Task DeleteAsync(string subject, bool administrator, Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
