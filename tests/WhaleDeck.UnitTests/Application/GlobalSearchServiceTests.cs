using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;
using WhaleDeck.Application.Services;
using WhaleDeck.Domain.Entities;

namespace WhaleDeck.UnitTests.Application;

public sealed class GlobalSearchServiceTests
{
    [Fact]
    public async Task RegularUsersOnlyReceiveVisiblePortalMatches()
    {
        var repository = new SearchRepositoryStub();
        var identity = new IdentityDirectoryStub();
        var service = new GlobalSearchService(new PortalRepositoryStub(), repository, identity);

        var results = await service.SearchAsync("user-1", false, "git", 20, default);

        var result = Assert.Single(results);
        Assert.Equal("Portal", result.Kind);
        Assert.False(repository.WasCalled);
        Assert.False(identity.WasCalled);
    }

    [Fact]
    public async Task AdministratorsReceiveManagedAndIdentityMatches()
    {
        var repository = new SearchRepositoryStub();
        var identity = new IdentityDirectoryStub();
        var service = new GlobalSearchService(new PortalRepositoryStub(), repository, identity);

        var results = await service.SearchAsync("admin", true, "dev", 20, default);

        Assert.Contains(results, item => item.Kind == "Container");
        Assert.Contains(results, item => item.Kind == "User");
        Assert.True(repository.WasCalled);
        Assert.True(identity.WasCalled);
    }

    [Theory]
    [InlineData("x")]
    [InlineData("line\nbreak")]
    public async Task SearchRejectsInvalidQueries(string query)
    {
        var service = new GlobalSearchService(new PortalRepositoryStub(), new SearchRepositoryStub(), new IdentityDirectoryStub());
        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync("subject", false, query, 20, default));
    }

    private sealed class PortalRepositoryStub : IPortalRepository
    {
        public Task<IReadOnlyCollection<PortalItem>> ListVisibleAsync(string subject, bool administrator, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<PortalItem>>([
                new PortalItem { Scope = "Public", Name = "GitLab", Description = "Git service", Url = "http://git.local", IconValue = "git" }
            ]);

        public Task<PortalItem> SaveAsync(string subject, bool administrator, SavePortalItemCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(string subject, bool administrator, Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class SearchRepositoryStub : IGlobalSearchRepository
    {
        public bool WasCalled { get; private set; }
        public Task<IReadOnlyCollection<GlobalSearchResultDto>> SearchAdministrationAsync(string query, int take, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult<IReadOnlyCollection<GlobalSearchResultDto>>([
                new("resource:1", "Container", "development-api", "container-1", "/containers", "Running", false)
            ]);
        }
    }

    private sealed class IdentityDirectoryStub : IIdentityDirectory
    {
        public bool WasCalled { get; private set; }
        public Task<IReadOnlyCollection<ManagedResourceDto>> ListUsersAsync(CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult<IReadOnlyCollection<ManagedResourceDto>>([
                new("user-1", "developer", "User", "Active", string.Empty, false, new Dictionary<string, string>())
            ]);
        }

        public Task<CurrentUserDto> ResolveCurrentAsync(string subject, string? name, IReadOnlyCollection<string> claimGroups, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<ManagedResourceDto>> ListGroupsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<ManagedResourceDto>> ListSsoApplicationsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
