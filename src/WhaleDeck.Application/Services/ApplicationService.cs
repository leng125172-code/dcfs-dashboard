using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;
using WhaleDeck.Domain.Entities;

namespace WhaleDeck.Application.Services;

public sealed class ApplicationService(IApplicationRepository repository)
{
    public async Task<IReadOnlyCollection<ApplicationInstallationDto>> ListAsync(CancellationToken cancellationToken) =>
        (await repository.ListAsync(cancellationToken)).Select(Map).ToArray();

    public async Task<ApplicationDetailDto?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var installation = await repository.FindAsync(id, cancellationToken);
        if (installation is null) return null;
        var resources = (await repository.ListResourcesAsync(id, cancellationToken))
            .Select(item => new ApplicationResourceDto(item.Role, item.Resource.Id, item.Resource.ExternalId,
                item.Resource.DisplayName, item.Resource.ResourceType, ReadState(item.Resource.LabelsJson),
                ReadVersion(item.Resource.LabelsJson), item.Resource.ProtectionLevel is "Protected" or "ControlPlane"))
            .ToArray();
        var updates = (await repository.ListUpdateRunsAsync(id, 100, cancellationToken)).Select(Map).ToArray();
        return new ApplicationDetailDto(Map(installation), resources, updates);
    }

    private static ApplicationInstallationDto Map(ApplicationInstallation item) => new(
        item.Id, item.CatalogAppId, item.TemplateId, item.TemplateVersion, item.DisplayName,
        item.InstalledVersion, item.DesiredVersion, item.State, item.AutoUpdateEnabled,
        item.ConfigSummaryJson, item.InstalledBySubject, item.InstalledAtUtc, item.UpdatedAtUtc, item.Version);

    private static ApplicationUpdateRunDto Map(ApplicationUpdateRun item) => new(
        item.Id, item.ApplicationId, item.OldImageDigest, item.NewImageDigest, item.VersionPolicy,
        item.PlanHash, item.JobId, item.StartedAtUtc, item.CompletedAtUtc, item.Result,
        item.WasRolledBack, item.ErrorSummary);

    private static string ReadState(string json) => ReadString(json, "state");
    private static string ReadVersion(string json) => ReadString(json, "version");

    private static string ReadString(string json, string property)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;
        }
        catch (System.Text.Json.JsonException)
        {
            return string.Empty;
        }
    }
}
